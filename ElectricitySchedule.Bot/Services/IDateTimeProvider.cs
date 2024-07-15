namespace ElectricitySchedule.Bot.Services;

internal interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}