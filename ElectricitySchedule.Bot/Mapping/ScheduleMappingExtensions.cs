using ElectricitySchedule.Bot.Models;
using ElectricitySchedule.Contracts;

namespace ElectricitySchedule.Bot.Mapping;

internal static class ScheduleMappingExtensions
{
    public static ScheduleModel ToModel(this SchedulePayload payload)
    {
        return new()
        {
            FetchedAt = payload.FetchedAt,
            Queues = payload.Queues.ConvertAll(q => q.ToModel()),
        };
    }
}