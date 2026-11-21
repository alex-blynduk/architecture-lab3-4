using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTracker.TaskService.Data;
using TaskTracker.TaskService.Domain;
using TaskTracker.TaskService.Dto;

namespace TaskTracker.TaskService.Controllers;

/// <summary>
/// Заготовка REST API контролера для мікросервісу завдань.
/// На наступному етапі сюди буде підключено Classic REST та Event-Driven (RabbitMQ) сценарії.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly TaskDbContext _db;

    public TasksController(TaskDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.Tasks
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => MapToResponse(t, 0))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _db.Tasks.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(MapToResponse(item, 0));
    }

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Назва завдання обов'язкова.");
        }

        var item = new TaskItem
        {
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Assignee = string.IsNullOrWhiteSpace(request.Assignee) ? "Unassigned" : request.Assignee.Trim(),
            Priority = request.Priority,
            Status = TaskItemStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tasks.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        // TODO (наступний крок): викликати NotificationService у режимі Classic REST або опублікувати подію у RabbitMQ.
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, MapToResponse(item, 0));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<TaskResponse>> ChangeStatus(
        Guid id,
        [FromBody] ChangeTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var item = await _db.Tasks.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        item.Status = request.NewStatus;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // TODO (наступний крок): надіслати сповіщення про зміну статусу (Classic або EventDriven).
        return Ok(MapToResponse(item, 0));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await _db.Tasks.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        _db.Tasks.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        // TODO (наступний крок): опублікувати критичну подію TaskDeleted.
        return NoContent();
    }

    private static TaskResponse MapToResponse(TaskItem item, long elapsedMs) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Description = item.Description,
        Assignee = item.Assignee,
        Priority = item.Priority,
        Status = item.Status,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
        ElapsedMs = elapsedMs
    };
}
