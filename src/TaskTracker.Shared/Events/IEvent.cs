namespace TaskTracker.Shared.Events;

/// <summary>
/// Базовий маркерний інтерфейс для всіх доменних подій агрегату Task.
/// </summary>
public interface IEvent
{
    Guid TaskId { get; }
    DateTime OccurredAt { get; }
}
