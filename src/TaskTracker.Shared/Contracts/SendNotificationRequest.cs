namespace TaskTracker.Shared.Contracts;

/// <summary>
/// DTO для синхронної (Classic REST) передачі запиту на сповіщення
/// від TaskService до NotificationService.
/// </summary>
public class SendNotificationRequest
{
    public Guid TaskId { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
