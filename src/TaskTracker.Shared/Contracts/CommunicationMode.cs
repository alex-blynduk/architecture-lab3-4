using System.Text.Json.Serialization;

namespace TaskTracker.Shared.Contracts;

/// <summary>
/// Режим взаємодії між TaskService та NotificationService
/// для порівняння підходів у межах ЛР №3.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CommunicationMode
{
    /// <summary>Синхронний HTTP/REST-виклик напряму до NotificationService.</summary>
    Classic = 0,

    /// <summary>Асинхронна публікація події у брокер RabbitMQ (хореографія).</summary>
    EventDriven = 1
}
