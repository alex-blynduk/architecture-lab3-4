using TaskTracker.Shared.Contracts;
using TaskTracker.TaskService.Dto;

namespace TaskTracker.TaskService.Services;

/// <summary>
/// Контракт прикладного сервісу управління завданнями.
/// </summary>
public interface ITaskAppService
{
    Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TaskResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TaskResponse> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);
    Task<TaskResponse?> ChangeStatusAsync(Guid id, ChangeTaskStatusRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CommunicationMode mode, string reason, CancellationToken cancellationToken = default);
}
