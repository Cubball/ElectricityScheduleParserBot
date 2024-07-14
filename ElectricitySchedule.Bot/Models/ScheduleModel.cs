namespace ElectricitySchedule.Bot.Models;

public class ScheduleModel
{
    public DateTime FetchedAt { get; set; }

    public List<QueueModel> Queues { get; set; } = default!;
}