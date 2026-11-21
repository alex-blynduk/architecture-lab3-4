using System.Text.Json.Serialization;

namespace TaskTracker.TaskService.Domain;

/// <summary>
/// Пріоритет виконання завдання.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
