using Microsoft.EntityFrameworkCore;
using ElectricitySchedule.Bot.Entities;

namespace ElectricitySchedule.Bot.Persistence;

internal class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<Queue> Queues { get; set; } = default!;

    public DbSet<SubscribedUser> SubscribedUsers { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubscribedUser>()
            .Property(u => u.TelegramId)
            .ValueGeneratedNever();

        modelBuilder.Entity<SubscribedUser>()
            .HasKey(u => u.TelegramId);
    }
}