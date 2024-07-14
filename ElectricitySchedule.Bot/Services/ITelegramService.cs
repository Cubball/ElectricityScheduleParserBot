namespace ElectricitySchedule.Bot.Services;

public interface ITelegramService
{
    Task NotifyAboutUpdatesAsync();

    Task HandleMessage(long userId, string text);
}