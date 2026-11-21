using System.Text.Json.Serialization;

namespace TaskTracker.TaskService.Domain;

/// <summary>
/// Статус життєвого циклу завдання (відповідає ЛР №1: Нове -> В роботі -> На перевірці -> Завершено).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskItemStatus
{
    New = 0,
    InProgress = 1,
    InReview = 2,
    Done = 3
}
