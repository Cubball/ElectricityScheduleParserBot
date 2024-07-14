namespace ElectricitySchedule.Bot.Entities;

internal class Queue
{
    public int Id { get; set; }

    public int Number { get; set; }

    public DateOnly Date { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string DisconnectionTimes { get; set; } = default!;
}