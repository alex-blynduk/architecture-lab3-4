using TaskTracker.NotificationService.Domain;
using TaskTracker.Shared.Contracts;

namespace TaskTracker.NotificationService.Services;

/// <summary>
/// Спільний контракт обробки сповіщень, незалежно від того,
/// чи запит прийшов синхронно (REST), чи асинхронно (RabbitMQ).
/// </summary>
public interface INotificationProcessor
{
    Task<NotificationLog> ProcessAsync(
        Guid taskId,
        string recipient,
        string eventType,
        string message,
        CommunicationMode mode,
        CancellationToken cancellationToken = default);
}
