using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Shared.Contracts;
using TaskTracker.Shared.Events;
using TaskTracker.TaskService.Data;
using TaskTracker.TaskService.Domain;
using TaskTracker.TaskService.Dto;

namespace TaskTracker.TaskService.Services;

/// <summary>
/// Бізнес-логіка мікросервісу TaskService.
/// Реалізує одні й ті самі бізнес-операції (створення, зміна статусу, видалення завдання)
/// двома способами для порівняння у ЛР №3:
/// 1) Classic (синхронний REST-виклик до NotificationService)
/// 2) EventDriven (асинхронна публікація події в RabbitMQ)
/// </summary>
public class TaskAppService : ITaskAppService
{
    private readonly TaskDbContext _db;
    private readonly INotificationClient _notificationClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<TaskAppService> _logger;

    public TaskAppService(
        TaskDbContext db,
        INotificationClient notificationClient,
        IEventPublisher eventPublisher,
        ILogger<TaskAppService> logger)
    {
        _db = db;
        _notificationClient = notificationClient;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Tasks
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => MapToResponse(t, 0))
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _db.Tasks.FindAsync([id], cancellationToken);
        return item is null ? null : MapToResponse(item, 0);
    }

    public async Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Activity.Current?.SetTag("communication.mode", request.Mode.ToString());

        var item = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Assignee = string.IsNullOrWhiteSpace(request.Assignee) ? "Unassigned" : request.Assignee.Trim(),
            Priority = request.Priority,
            Status = TaskItemStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        var message = $"Створено нове завдання «{item.Title}» (пріоритет: {item.Priority}).";

        if (request.Mode == CommunicationMode.Classic)
        {
            // Класичний синхронний підхід: зберігаємо в транзакції і чекаємо відповіді NotificationService.
            // Якщо NotificationService падає — відкочуємо транзакцію (сильна зв'язність!).
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            _db.Tasks.Add(item);
            await _db.SaveChangesAsync(cancellationToken);

            await _notificationClient.SendAsync(new SendNotificationRequest
            {
                TaskId = item.Id,
                Recipient = item.Assignee,
                EventType = nameof(TaskCreated),
                Message = message
            }, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        else
        {
            // Подієво-орієнтований підхід: зберігаємо завдання та публікуємо подію у RabbitMQ.
            // Клієнт одразу отримує відповідь без очікування NotificationService (слабка зв'язність).
            _db.Tasks.Add(item);
            await _db.SaveChangesAsync(cancellationToken);

            _eventPublisher.Publish(new TaskCreated
            {
                TaskId = item.Id,
                Title = item.Title,
                Assignee = item.Assignee,
                Priority = item.Priority.ToString(),
                OccurredAt = DateTime.UtcNow
            });
        }

        sw.Stop();
        _logger.LogInformation(
            "[TaskService] Створено завдання {TaskId} ({Title}) у режимі {Mode} за {ElapsedMs} мс.",
            item.Id, item.Title, request.Mode, sw.ElapsedMilliseconds);

        return MapToResponse(item, sw.ElapsedMilliseconds);
    }

    public async Task<TaskResponse?> ChangeStatusAsync(
        Guid id,
        ChangeTaskStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Activity.Current?.SetTag("communication.mode", request.Mode.ToString());

        var item = await _db.Tasks.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return null;
        }

        var oldStatus = item.Status;
        item.Status = request.NewStatus;
        item.UpdatedAt = DateTime.UtcNow;

        var message = $"Статус завдання «{item.Title}» змінено: {oldStatus} -> {request.NewStatus}.";

        if (request.Mode == CommunicationMode.Classic)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            await _notificationClient.SendAsync(new SendNotificationRequest
            {
                TaskId = item.Id,
                Recipient = item.Assignee,
                EventType = nameof(TaskStatusChanged),
                Message = message
            }, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        else
        {
            await _db.SaveChangesAsync(cancellationToken);

            _eventPublisher.Publish(new TaskStatusChanged
            {
                TaskId = item.Id,
                Title = item.Title,
                Assignee = item.Assignee,
                OldStatus = oldStatus.ToString(),
                NewStatus = request.NewStatus.ToString(),
                OccurredAt = DateTime.UtcNow
            });
        }

        sw.Stop();
        _logger.LogInformation(
            "[TaskService] Змінено статус завдання {TaskId} ({Old} -> {New}) у режимі {Mode} за {ElapsedMs} мс.",
            item.Id, oldStatus, request.NewStatus, request.Mode, sw.ElapsedMilliseconds);

        return MapToResponse(item, sw.ElapsedMilliseconds);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CommunicationMode mode,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Activity.Current?.SetTag("communication.mode", mode.ToString());

        var item = await _db.Tasks.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return false;
        }

        var safeReason = string.IsNullOrWhiteSpace(reason) ? "Видалено користувачем" : reason.Trim();
        var message = $"Завдання «{item.Title}» видалено. Причина: {safeReason}.";

        if (mode == CommunicationMode.Classic)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            _db.Tasks.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);

            await _notificationClient.SendAsync(new SendNotificationRequest
            {
                TaskId = item.Id,
                Recipient = item.Assignee,
                EventType = nameof(TaskDeleted),
                Message = message
            }, cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        else
        {
            _db.Tasks.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);

            _eventPublisher.Publish(new TaskDeleted
            {
                TaskId = item.Id,
                Title = item.Title,
                Assignee = item.Assignee,
                Reason = safeReason,
                OccurredAt = DateTime.UtcNow
            });
        }

        sw.Stop();
        _logger.LogInformation(
            "[TaskService] Видалено завдання {TaskId} ({Title}) у режимі {Mode} за {ElapsedMs} мс.",
            item.Id, item.Title, mode, sw.ElapsedMilliseconds);

        return true;
    }

    private static TaskResponse MapToResponse(TaskItem item, long elapsedMs) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        Assignee = item.Assignee,
        Priority = item.Priority,
        Status = item.Status,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
        ElapsedMs = elapsedMs
    };
}
