using System.Net.Http.Json;
using Amazon.Lambda.Core;
using ElectricitySchedule.Contracts;
using HtmlAgilityPack;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ElectricitySchedule.Parser;

public class Function
{
    private const string DateFormat = "dd.MM.yyyy";
    private const char DisconnectionTimesSeparator = ';';
    private const int HeaderTableRowsCount = 3;

    public async Task FunctionHandler(ILambdaContext context)
    {
        try
        {
            await Handle(context);
        }
        catch (Exception e)
        {
            context.Logger.LogError(e.Message);
        }
    }

    private static async Task Handle(ILambdaContext context)
    {
        var address = Environment.GetEnvironmentVariable("SCHEDULE_WEB_PAGE");
        if (string.IsNullOrWhiteSpace(address))
        {
            context.Logger.LogError("SCHEDULE_WEB_PAGE is not set");
            return;
        }

        var botLambdaEndpoint = Environment.GetEnvironmentVariable("BOT_LAMBDA_ENDPOINT");
        if (string.IsNullOrWhiteSpace(botLambdaEndpoint))
        {
            context.Logger.LogError("BOT_LAMBDA_ENDPOINT is not set");
            return;
        }

        var htmlWeb = new HtmlWeb();
        var htmlDocument = await htmlWeb.LoadFromWebAsync(address);
        context.Logger.LogInformation("Loaded the web page");
        var schedule = GetScheduleFromHtmlDocument(htmlDocument, context.Logger);
        context.Logger.LogInformation("Parsed the web page");
        if (schedule is null)
        {
            return;
        }
        using var httpClient = new HttpClient();
        await httpClient.PostAsJsonAsync(botLambdaEndpoint, schedule);
        context.Logger.LogInformation("Sent the schedule");
    }

    private static SchedulePayload? GetScheduleFromHtmlDocument(HtmlDocument htmlDocument, ILambdaLogger logger)
    {
        var tbody = htmlDocument.DocumentNode.SelectSingleNode("//tbody");
        if (tbody is null)
        {
            logger.LogError("The HTML document does not contain a tbody element");
            return null;
        }

        var trs = tbody.ChildNodes
            .Where(n => n.Name == "tr")
            .Skip(HeaderTableRowsCount)
            .ToArray();
        if (trs.Length == 0)
        {
            logger.LogError("The table does not contain rows with disconnection times");
            return null;
        }

        var queues = new List<QueuePayload>();
        foreach (var tr in trs)
        {
            ParseScheduleFromTableRow(tr, queues, logger);
        }

        var schedule = new SchedulePayload
        {
            Queues = queues,
            FetchedAt = DateTime.UtcNow,
        };
        return schedule;
    }

    private static void ParseScheduleFromTableRow(HtmlNode tr, List<QueuePayload> queues, ILambdaLogger logger)
    {
        var children = tr.ChildNodes
            .Where(n => n.NodeType == HtmlNodeType.Element)
            .ToList();
        if (children.Count == 0)
        {
            logger.LogError("Table row does not contain children");
            return;
        }

        var dateText = children[0].InnerText.Trim();
        var dateOnly = DateOnly.ParseExact(dateText, DateFormat);
        for (int i = 1; i < children.Count; i++)
        {
            ParseTableCell(children[i].InnerText, i, dateOnly, queues);
        }
    }

    private static void ParseTableCell(string text, int queueNumber, DateOnly dateOnly, List<QueuePayload> queues)
    {
        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Any(c => char.IsDigit(c)))
            .Select(l => l.Trim());
        var disconnectionTimes = string.Join(DisconnectionTimesSeparator, lines);
        if (!string.IsNullOrWhiteSpace(disconnectionTimes))
        {
            queues.Add(new() { Number = queueNumber, Date = dateOnly, DisconnectionTimes = disconnectionTimes });
        }
    }
}