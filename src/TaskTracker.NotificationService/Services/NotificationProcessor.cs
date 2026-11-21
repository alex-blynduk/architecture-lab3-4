using System.Diagnostics;
using TaskTracker.NotificationService.Data;
using TaskTracker.NotificationService.Domain;
using TaskTracker.Shared.Contracts;

namespace TaskTracker.NotificationService.Services;

/// <summary>
/// Єдина точка обробки сповіщень для обох режимів взаємодії:
/// - Classic (синхронний REST-запит від TaskService)
/// - EventDriven (асинхронна подія з RabbitMQ)
/// </summary>
public class NotificationProcessor : INotificationProcessor
{
    private readonly NotificationDbContext _db;
    private readonly SimulationOptions _simulation;
    private readonly ILogger<NotificationProcessor> _logger;

    public NotificationProcessor(
        NotificationDbContext db,
        SimulationOptions simulation,
        ILogger<NotificationProcessor> logger)
    {
        _db = db;
        _simulation = simulation;
        _logger = logger;
    }

    public async Task<NotificationLog> ProcessAsync(
        Guid taskId,
        string recipient,
        string eventType,
        string message,
        CommunicationMode mode,
        CancellationToken cancellationToken = default)
    {
        Activity.Current?.SetTag("notification.mode", mode.ToString());
        Activity.Current?.SetTag("notification.event_type", eventType);
        Activity.Current?.SetTag("notification.recipient", recipient);
        Activity.Current?.SetTag("task.id", taskId.ToString());

        // Імітація затримки зовнішнього Email/SMTP провайдера (для наочного порівняння затримок у ЛР №3)
        if (_simulation.DelayMs > 0)
        {
            Activity.Current?.SetTag("simulation.delay_ms", _simulation.DelayMs);
            await Task.Delay(_simulation.DelayMs, cancellationToken);
        }

        // Імітація збою сервісу (для демонстрації сценарію з помилкою в Jaeger)
        if (_simulation.SimulateFailure)
        {
            const string errorMessage = "Симульований збій NotificationService (імітація недоступності провайдера сповіщень).";
            Activity.Current?.SetStatus(ActivityStatusCode.Error, errorMessage);
            Activity.Current?.SetTag("error", true);
            _logger.LogError("[NotificationService] [{Mode}] ПОМИЛКА обробки {EventType} для завдання {TaskId}: {Error}",
                mode, eventType, taskId, errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        var log = new NotificationLog
        {
            TaskId = taskId,
            Recipient = string.IsNullOrWhiteSpace(recipient) ? "Unassigned" : recipient,
            EventType = eventType,
            Message = message,
            DeliveryMode = mode,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[NotificationService] [{Mode}] Надіслано сповіщення -> {Recipient} | {EventType}: {Message}",
            mode, log.Recipient, log.EventType, log.Message);

        return log;
    }
}
