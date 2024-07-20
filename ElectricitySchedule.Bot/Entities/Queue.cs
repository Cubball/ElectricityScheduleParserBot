using MongoDB.Bson;

namespace ElectricitySchedule.Bot.Entities;

internal class Queue
{
    public ObjectId Id { get; set; }

    public int Number { get; set; }

    public DateTime Date { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string DisconnectionTimes { get; set; } = default!;
}