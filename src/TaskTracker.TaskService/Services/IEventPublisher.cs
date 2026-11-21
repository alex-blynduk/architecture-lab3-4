using TaskTracker.Shared.Events;

namespace TaskTracker.TaskService.Services;

/// <summary>
/// Контракт видавця доменних подій у брокер повідомлень (Event-Driven режим).
/// </summary>
public interface IEventPublisher
{
    void Publish(IEvent @event);
}
