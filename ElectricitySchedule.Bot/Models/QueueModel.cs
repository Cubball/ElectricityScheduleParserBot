namespace ElectricitySchedule.Bot.Models;

public class QueueModel
{
    public int Number { get; set; }

    public DateOnly Date { get; set; }

    public string DisconnectionTimes { get; set; } = default!;
}