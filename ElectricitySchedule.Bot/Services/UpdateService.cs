using ElectricitySchedule.Bot.Entities;
using ElectricitySchedule.Bot.Models;
using ElectricitySchedule.Bot.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectricitySchedule.Bot.Services;

internal class UpdateService(
    ILogger<UpdateService> logger,
    ApplicationDbContext dbContext) : IUpdateService
{
    private readonly ILogger<UpdateService> _logger = logger;
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<bool> UpdateScheduleAsync(ScheduleModel schedule)
    {
        var queues = await _dbContext.Queues.ToListAsync();
        _logger.LogInformation("Retrieved {QueuesCount} queues from the database", queues.Count);
        var queuesToDelete = new List<Queue>();
        foreach (var queue in queues)
        {
            var newQueue = schedule.Queues.FirstOrDefault(q => q.Number == queue.Number && q.Date == DateOnly.FromDateTime(queue.Date));
            if (newQueue is null)
            {
                queuesToDelete.Add(queue);
                continue;
            }

            if (queue.DisconnectionTimes != newQueue.DisconnectionTimes)
            {
                _logger.LogInformation("Queue #{QueueNumber} on {QueueDate} has changes, updating...", queue.Number, DateOnly.FromDateTime(queue.Date));
                queue.DisconnectionTimes = newQueue.DisconnectionTimes;
                queue.UpdatedAt = schedule.FetchedAt;
            }

            schedule.Queues.Remove(newQueue);
        }

        _logger.LogInformation(
            "{QueuesToDeleteCount} will be deleted, {QueuesToAddCount} will be added",
            queuesToDelete.Count,
            schedule.Queues.Count);
        _dbContext.Queues.RemoveRange(queuesToDelete);
        _dbContext.Queues.AddRange(schedule.Queues.Select(q => new Queue
        {
            Date = q.Date.ToDateTime(new TimeOnly()),
            Number = q.Number,
            UpdatedAt = schedule.FetchedAt,
            DisconnectionTimes = q.DisconnectionTimes,
        }));
        try
        {
            var modifiedCount = await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Saved changes successfully, modified count: {ModifiedCount}", modifiedCount);
            return modifiedCount > 0;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to update the database");
            return false;
        }
    }
}