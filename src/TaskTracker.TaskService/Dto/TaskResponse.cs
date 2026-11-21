using TaskTracker.TaskService.Domain;

namespace TaskTracker.TaskService.Dto;

/// <summary>
/// Відповідь API із даними завдання та заміром часу виконання операції (для порівняння Classic vs Event-Driven).
/// </summary>
public class TaskResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; }
    public TaskItemStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long ElapsedMs { get; set; }
}
