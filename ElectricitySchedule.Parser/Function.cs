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

    public async Task FunctionHandler(ILambdaContext context)
    {
        context.Logger.LogInformation($"Invoked at {DateTime.UtcNow}");
        var address = Environment.GetEnvironmentVariable("SCHEDULE_WEB_PAGE");
        if (string.IsNullOrWhiteSpace(address))
        {
            context.Logger.LogError("The address of a web page with the schedule is not provided. Make sure that the 'SCHEDULE_WEB_PAGE' environment variable is set");
            return;
        }

        var htmlWeb = new HtmlWeb();
        var htmlDocument = await htmlWeb.LoadFromWebAsync(address);
        var schedule = GetScheduleFromHtmlDocument(htmlDocument, context.Logger);
        if (schedule is null)
        {
            return;
        }

        var botLambdaEndpoint = Environment.GetEnvironmentVariable("BOT_LAMBDA_ENDPOINT");
        if (string.IsNullOrWhiteSpace(botLambdaEndpoint))
        {
            context.Logger.LogError("The endpoint of a consumer lambda is not provided. Make sure that the 'BOT_LAMBDA_ENDPOINT' environment variable is set");
            return;
        }

        using var httpClient = new HttpClient();
        await httpClient.PostAsJsonAsync(botLambdaEndpoint, schedule);
        context.Logger.LogInformation("Successfully parsed and sent the schedule");
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
            .ToArray();
        if (trs.Length < 2)
        {
            logger.LogError($"Expected to get at least 2 table rows, but got {trs.Length}");
            return null;
        }

        var firstDay = trs[^2];
        var secondDay = trs[^1];
        var queues = new List<QueuePayload>();
        ParseScheduleFromTableRow(firstDay, queues, logger);
        ParseScheduleFromTableRow(secondDay, queues, logger);
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