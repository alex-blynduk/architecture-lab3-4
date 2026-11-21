namespace TaskTracker.Shared.Events;

/// <summary>
/// Подія: завдання видалено (критична подія).
/// </summary>
public class TaskDeleted : IEvent
{
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
