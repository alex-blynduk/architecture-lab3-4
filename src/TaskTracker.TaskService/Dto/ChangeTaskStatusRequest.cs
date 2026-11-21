using TaskTracker.Shared.Contracts;
using TaskTracker.TaskService.Domain;

namespace TaskTracker.TaskService.Dto;

/// <summary>
/// Запит на зміну статусу завдання з вибором режиму комунікації.
/// </summary>
public class ChangeTaskStatusRequest
{
    public TaskItemStatus NewStatus { get; set; }
    public CommunicationMode Mode { get; set; } = CommunicationMode.EventDriven;
}
