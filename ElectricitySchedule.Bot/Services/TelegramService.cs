using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ElectricitySchedule.Bot.Entities;
using ElectricitySchedule.Bot.Persistence;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ElectricitySchedule.Bot.Services;

internal class TelegramService(ApplicationDbContext dbContext, ITelegramBotClient telegramBotClient) : ITelegramService
{
    // TODO: move these into config?
    private const string DateFormat = "dd.MM.yyyy";
    private const char DisconnectionTimesSeparator = ';';
    private const int QueuesCount = 6;

    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly ITelegramBotClient _telegramBotClient = telegramBotClient;

    public Task HandleMessage(long userId, string text)
    {
        // NOTE: DRY later maybe
        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            return HandleStartCommand(userId);
        }

        if (text.StartsWith("/queue", StringComparison.OrdinalIgnoreCase))
        {
            return HandleQueueCommand(userId, text[6..]);
        }

        if (text.StartsWith("/stop", StringComparison.OrdinalIgnoreCase))
        {
            return HandleStopCommand(userId);
        }

        return _telegramBotClient.SendTextMessageAsync(new ChatId(userId), "Дана команда/повідомлення не підтримується");
    }

    public async Task NotifyAboutUpdatesAsync()
    {
        var users = await _dbContext.SubscribedUsers.ToListAsync();
        var queues = await _dbContext.Queues.ToListAsync();
        var tasks = new List<Task>();
        foreach (var user in users)
        {
            // TODO: notify only about changed schedules
            var usersQueues = user.QueueNumber is null
                ? queues
                : queues.Where(q => q.Number == user.QueueNumber).ToList();
            var latest = usersQueues.MaxBy(q => q.UpdatedAt)?.UpdatedAt;
            if (user.LastReceivedUpdate is null || latest > user.LastReceivedUpdate)
            {
                user.LastReceivedUpdate = latest;
                tasks.Add(SendUpdatedScheduleToUser(user, usersQueues));
            }
        }

        tasks.Add(_dbContext.SaveChangesAsync());
        await Task.WhenAll(tasks);
    }

    private Task SendUpdatedScheduleToUser(SubscribedUser user, List<Queue> usersQueues)
    {
        usersQueues.Sort((a, b) => a.Date.CompareTo(b.Date));
        var stringBuilder = new StringBuilder("Графіки змінилися:\n");
        foreach (var queue in usersQueues)
        {
            stringBuilder.Append('\n');
            stringBuilder.Append(queue.Date.ToString(DateFormat, CultureInfo.InvariantCulture));
            stringBuilder.Append('\n');
            stringBuilder.Append('\n');
            stringBuilder.Append(queue.DisconnectionTimes.Replace(DisconnectionTimesSeparator, '\n'));
            stringBuilder.Append('\n');
        }

        stringBuilder.Length--;
        return _telegramBotClient.SendTextMessageAsync(new ChatId(user.TelegramId), stringBuilder.ToString());
    }

    private async Task HandleStartCommand(long userId)
    {
        var user = new SubscribedUser
        {
            TelegramId = userId,
        };
        _dbContext.SubscribedUsers.Add(user);
        await _dbContext.SaveChangesAsync();
        await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), $"Тепер ви будете отримувати оновлення графіка відключень!. Щоб змінити номер черги, скористайтеся командою /queue, щоб припинити отримувати оновлення, скористайтеся командою /stop");
    }

    private async Task HandleQueueCommand(long userId, string text)
    {
        var parsed = int.TryParse(text.Trim(), out var queueNumber);
        if (!parsed || queueNumber < 0 || queueNumber > QueuesCount)
        {
            await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), $"Номер черги має бути числом від 0 до {QueuesCount}");
            return;
        }

        var user = await _dbContext.SubscribedUsers.FirstAsync(u => u.TelegramId == userId);
        if (user is null)
        {
            await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), "Щоб почати отримувати оновлення, скористайтеся командою /start");
            return;
        }

        user.QueueNumber = queueNumber == 0 ? null : queueNumber;
        await _dbContext.SaveChangesAsync();
        await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), "Чергу змінено!");
    }

    private async Task HandleStopCommand(long userId)
    {
        var user = await _dbContext.SubscribedUsers.FirstOrDefaultAsync(u => u.TelegramId == userId);
        if (user is not null)
        {
            _dbContext.SubscribedUsers.Remove(user);
            await _dbContext.SaveChangesAsync();
        }

        await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), "Тепер ви не будете отримувати оновлення графіка відключень");
    }
}