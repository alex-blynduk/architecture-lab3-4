using TaskTracker.Shared.Contracts;
using TaskTracker.TaskService.Domain;

namespace TaskTracker.TaskService.Dto;

/// <summary>
/// Запит на створення нового завдання з вибором способу взаємодії
/// між сервісами (Classic REST або Event-Driven через RabbitMQ).
/// </summary>
public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public CommunicationMode Mode { get; set; } = CommunicationMode.EventDriven;
}
