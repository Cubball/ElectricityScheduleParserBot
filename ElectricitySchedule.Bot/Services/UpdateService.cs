using Microsoft.EntityFrameworkCore;
using ElectricitySchedule.Bot.Entities;
using ElectricitySchedule.Bot.Models;
using ElectricitySchedule.Bot.Persistence;

namespace ElectricitySchedule.Bot.Services;

internal class UpdateService(ApplicationDbContext dbContext) : IUpdateService
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<bool> UpdateScheduleAsync(ScheduleModel schedule)
    {
        var queues = await _dbContext.Queues.ToListAsync();
        var queuesToDelete = new List<Queue>();
        foreach (var queue in queues)
        {
            var newQueue = schedule.Queues.FirstOrDefault(q => q.Number == queue.Number && q.Date == queue.Date);
            if (newQueue is null)
            {
                queuesToDelete.Add(queue);
                continue;
            }

            if (queue.DisconnectionTimes != newQueue.DisconnectionTimes)
            {
                queue.DisconnectionTimes = newQueue.DisconnectionTimes;
                queue.UpdatedAt = schedule.FetchedAt;
            }

            schedule.Queues.Remove(newQueue);
        }

        _dbContext.Queues.RemoveRange(queuesToDelete);
        _dbContext.Queues.AddRange(schedule.Queues.Select(q => new Queue
        {
            Date = q.Date,
            Number = q.Number,
            UpdatedAt = schedule.FetchedAt,
            DisconnectionTimes = q.DisconnectionTimes,
        }));
        var modifiedCount = await _dbContext.SaveChangesAsync();
        return modifiedCount > 0;
    }
}