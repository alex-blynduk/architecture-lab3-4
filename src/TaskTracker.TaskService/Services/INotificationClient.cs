using TaskTracker.Shared.Contracts;

namespace TaskTracker.TaskService.Services;

/// <summary>
/// Контракт синхронного HTTP-клієнта до NotificationService (Classic REST режим).
/// </summary>
public interface INotificationClient
{
    Task SendAsync(SendNotificationRequest request, CancellationToken cancellationToken = default);
}
