using System.Globalization;
using System.Text;
using ElectricitySchedule.Bot.Entities;
using ElectricitySchedule.Bot.Persistence;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ElectricitySchedule.Bot.Services;

internal class TelegramService(
    ILogger<TelegramService> logger,
    ApplicationDbContext dbContext,
    ITelegramBotClient telegramBotClient,
    IDateTimeProvider dateTimeProvider) : ITelegramService
{
    private const string DateFormat = "dd.MM.yyyy";
    private const char DisconnectionTimesSeparator = ';';
    private const int QueuesCount = 6;

    private readonly ILogger<TelegramService> _logger = logger;
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly ITelegramBotClient _telegramBotClient = telegramBotClient;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

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

        _logger.LogInformation("Received an unsupported message from {UserId} with contents \"{MessageContent}\"", userId, text);
        return _telegramBotClient.SendTextMessageAsync(new ChatId(userId), "Дана команда/повідомлення не підтримується");
    }

    public async Task NotifyAboutUpdatesAsync()
    {
        var users = await _dbContext.SubscribedUsers.ToListAsync();
        var queues = await _dbContext.Queues.ToListAsync();
        _logger.LogInformation("Retrieved {UsersCount} users and {QueuesCount} queues from the database", users.Count, queues.Count);
        var tasks = new List<Task>();
        foreach (var user in users)
        {
            var usersQueues = queues.Where(q => q.UpdatedAt > user.LastReceivedUpdate);
            if (user.QueueNumber is not null)
            {
                usersQueues = usersQueues.Where(q => q.Number == user.QueueNumber);
            }

            tasks.Add(SendUpdatedScheduleToUser(user, usersQueues.ToList()));
        }

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update the database");
            return;
        }

        await Task.WhenAll(tasks);
    }

    private Task SendUpdatedScheduleToUser(SubscribedUser user, List<Queue> usersQueues)
    {
        if (usersQueues.Count == 0)
        {
            return Task.CompletedTask;
        }

        usersQueues.Sort((a, b) =>
        {
            var result = a.Date.CompareTo(b.Date);
            return result != 0 ? result : a.Number.CompareTo(b.Number);
        });
        var messageText = GetMessageFromQueues(usersQueues);
        _logger.LogInformation(
            "Sending an update regarding {UpdatedQueuesCount} to the user with Id {UserId}",
            usersQueues.Count,
            user.TelegramId);
        return _telegramBotClient.SendTextMessageAsync(
            new ChatId(user.TelegramId),
            messageText.ToString(),
            parseMode: ParseMode.Html);
    }

    private async Task HandleStartCommand(long userId)
    {
        _logger.LogInformation("User with Id {userId} requested to start receiving updates", userId);
        var existingUser = await _dbContext.SubscribedUsers.FirstOrDefaultAsync(u => u.TelegramId == userId);
        if (existingUser is not null)
        {
            _logger.LogInformation("User with Id {userId} is already in the database", userId);
            await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), "Ви вже отримуєте оновлення графіка відключень");
            return;
        }

        var queues = await _dbContext.Queues.ToListAsync();
        var user = new SubscribedUser
        {
            TelegramId = userId,
            LastReceivedUpdate = _dateTimeProvider.UtcNow,
        };
        _dbContext.SubscribedUsers.Add(user);
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update the database");
            return;
        }

        await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), $"Тепер ви будете отримувати оновлення графіка відключень! Щоб змінити номер черги, скористайтеся командою \"/queue <номер черги>\" (наприклад \"/queue 3\"), щоб припинити отримувати оновлення, скористайтеся командою /stop");
        await SendUpdatedScheduleToUser(user, queues);
    }

    private async Task HandleQueueCommand(long userId, string text)
    {
        _logger.LogInformation("User with Id {userId} requested to change the queue number", userId);
        var parsed = int.TryParse(text.Trim(), out var queueNumber);
        if (!parsed || queueNumber < 0 || queueNumber > QueuesCount)
        {
            _logger.LogInformation("User with Id {userId} sent a wrong queue number: {QueueNumberText}", userId, text);
            await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), $"Номер черги має бути числом від 0 до {QueuesCount} (0 - щоб отримувати оновлення всіх черг)");
            return;
        }

        var user = await _dbContext.SubscribedUsers.FirstAsync(u => u.TelegramId == userId);
        if (user is null)
        {
            _logger.LogWarning("User with Id {userId} is not present in the database", userId);
            await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), "Щоб почати отримувати оновлення, скористайтеся командою /start");
            return;
        }

        user.QueueNumber = queueNumber == 0 ? null : queueNumber;
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogInformation(e, "Failed to update the user with Id {UserId}", userId);
            return;
        }

        await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), "Чергу змінено!");
    }

    private async Task HandleStopCommand(long userId)
    {
        _logger.LogInformation("User with Id {userId} requested to stop receiving updates", userId);
        var user = await _dbContext.SubscribedUsers.FirstOrDefaultAsync(u => u.TelegramId == userId);
        if (user is not null)
        {
            try
            {
                _dbContext.SubscribedUsers.Remove(user);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to delete a user with Id {userId} from the database", userId);
            }
        }
        else
        {
            _logger.LogWarning("User with Id {userId} is not present in the database", userId);
        }

        await _telegramBotClient.SendTextMessageAsync(new ChatId(userId), "Тепер ви не будете отримувати оновлення графіка відключень");
    }

    private static string GetMessageFromQueues(List<Queue> usersQueues)
    {
        var stringBuilder = new StringBuilder("<b>Графіки змінилися:</b>\n");
        foreach (var queue in usersQueues)
        {
            stringBuilder.Append('\n');
            stringBuilder.Append($"<u>Черга №{queue.Number} - {queue.Date.ToString(DateFormat, CultureInfo.InvariantCulture)}</u>\n\n");
            stringBuilder.Append(queue.DisconnectionTimes.Replace(DisconnectionTimesSeparator, '\n'));
            stringBuilder.Append('\n');
        }

        stringBuilder.Length--;
        return stringBuilder.ToString();
    }
}