namespace ElectricitySchedule.Bot.Entities;

internal class SubscribedUser
{
    public long TelegramId { get; set; } = default!;

    public int? QueueNumber { get; set; }

    public DateTime? LastReceivedUpdate { get; set; }
}