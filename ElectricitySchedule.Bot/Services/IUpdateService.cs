using ElectricitySchedule.Bot.Models;

namespace ElectricitySchedule.Bot.Services;

public interface IUpdateService
{
    Task<bool> UpdateScheduleAsync(ScheduleModel schedule);
}