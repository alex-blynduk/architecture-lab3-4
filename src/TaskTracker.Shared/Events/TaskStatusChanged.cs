namespace TaskTracker.Shared.Events;

/// <summary>
/// Подія: змінено статус виконання завдання.
/// </summary>
public class TaskStatusChanged : IEvent
{
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
