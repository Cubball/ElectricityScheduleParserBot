namespace ElectricitySchedule.Contracts;

public class QueuePayload
{
    public int Number { get; set; }

    public DateOnly Date { get; set; }

    public string DisconnectionTimes { get; set; } = default!;
}