namespace ElectricitySchedule.Contracts;

public class SchedulePayload
{
    public DateTime FetchedAt { get; set; }

    public List<QueuePayload> Queues { get; set; } = default!;
}