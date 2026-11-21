using Microsoft.EntityFrameworkCore;
using TaskTracker.NotificationService.Domain;

namespace TaskTracker.NotificationService.Data;

/// <summary>
/// Контекст EF Core для ізольованої бази даних NotificationService (SQLite).
/// </summary>
public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<NotificationLog> Notifications => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Recipient).IsRequired().HasMaxLength(100);
            entity.Property(n => n.EventType).IsRequired().HasMaxLength(100);
            entity.Property(n => n.Message).IsRequired().HasMaxLength(1000);
            entity.Property(n => n.DeliveryMode).HasConversion<string>();
        });
    }
}
