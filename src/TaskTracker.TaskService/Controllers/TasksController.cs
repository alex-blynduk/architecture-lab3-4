using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Shared.Contracts;
using TaskTracker.TaskService.Dto;
using TaskTracker.TaskService.Services;

namespace TaskTracker.TaskService.Controllers;

/// <summary>
/// REST API контролер мікросервісу завдань (TaskService).
/// Підтримує виконання бізнес-сценаріїв у двох режимах: Classic (REST) та EventDriven (RabbitMQ).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskAppService _taskService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ITaskAppService taskService, ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _taskService.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _taskService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new { error = "Назва завдання обов'язкова." });
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var created = await _taskService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            sw.Stop();
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Activity.Current?.SetTag("error", true);
            _logger.LogError(ex, "[TaskService] Синхронний збій при створенні завдання у режимі {Mode}.", request.Mode);

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = $"Збій синхронного виклику NotificationService (операцію відкочено): {ex.Message}",
                mode = request.Mode.ToString(),
                elapsedMs = sw.ElapsedMilliseconds
            });
        }
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<TaskResponse>> ChangeStatus(
        Guid id,
        [FromBody] ChangeTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var updated = await _taskService.ChangeStatusAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            sw.Stop();
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Activity.Current?.SetTag("error", true);
            _logger.LogError(ex, "[TaskService] Синхронний збій при зміні статусу завдання {TaskId}.", id);

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = $"Збій синхронного виклику NotificationService (зміну статусу відкочено): {ex.Message}",
                mode = request.Mode.ToString(),
                elapsedMs = sw.ElapsedMilliseconds
            });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] CommunicationMode mode = CommunicationMode.EventDriven,
        [FromQuery] string reason = "Видалено користувачем",
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var deleted = await _taskService.DeleteAsync(id, mode, reason, cancellationToken);
            if (!deleted)
            {
                return NotFound();
            }

            sw.Stop();
            return Ok(new { deleted = true, mode = mode.ToString(), elapsedMs = sw.ElapsedMilliseconds });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            sw.Stop();
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Activity.Current?.SetTag("error", true);
            _logger.LogError(ex, "[TaskService] Синхронний збій при видаленні завдання {TaskId}.", id);

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = $"Збій синхронного виклику NotificationService (видалення відкочено): {ex.Message}",
                mode = mode.ToString(),
                elapsedMs = sw.ElapsedMilliseconds
            });
        }
    }
}
