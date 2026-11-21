namespace TaskTracker.Shared.Events;

/// <summary>
/// Подія: створено нове завдання.
/// </summary>
public class TaskCreated : IEvent
{
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
