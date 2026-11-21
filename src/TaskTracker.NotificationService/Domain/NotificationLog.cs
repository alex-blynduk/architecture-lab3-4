using TaskTracker.Shared.Contracts;

namespace TaskTracker.NotificationService.Domain;

/// <summary>
/// Запис про надіслане сповіщення.
/// Зберігається у власній БД мікросервісу NotificationService (Database-per-Service, SQLite notifications.db).
/// </summary>
public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public CommunicationMode DeliveryMode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
