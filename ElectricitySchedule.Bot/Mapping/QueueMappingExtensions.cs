using ElectricitySchedule.Bot.Models;
using ElectricitySchedule.Contracts;

namespace ElectricitySchedule.Bot.Mapping;

internal static class QueueMappingExtensions
{
    public static QueueModel ToModel(this QueuePayload payload)
    {
        return new()
        {
            Number = payload.Number,
            Date = payload.Date,
            DisconnectionTimes = payload.DisconnectionTimes,
        };
    }
}