namespace TaskTracker.Shared.Messaging;

/// <summary>
/// Спільні константи для взаємодії сервісів через RabbitMQ.
/// Збережено структуру маршрутизації з ЛР №2 (topic exchange "task_events").
/// </summary>
public static class RabbitMqConstants
{
    public const string ExchangeName = "task_events";

    public const string TaskCreatedRoutingKey = "task.created";
    public const string TaskStatusChangedRoutingKey = "task.status_changed";
    public const string TaskDeletedRoutingKey = "task.deleted";

    public const string NotificationsQueueName = "task_events.notifications";
    public const string NotificationsBindingKey = "task.#";

    public const string EventTypeHeader = "EventType";
}
