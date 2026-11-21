using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTracker.NotificationService.Data;
using TaskTracker.NotificationService.Domain;
using TaskTracker.NotificationService.Services;
using TaskTracker.Shared.Contracts;

namespace TaskTracker.NotificationService.Controllers;

/// <summary>
/// Заготовка REST API контролера мікросервісу сповіщень.
/// Обслуговує синхронні запити від TaskService (Classic режим),
/// перегляд історії сповіщень та керування симуляцією затримки/збоїв.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationDbContext _db;
    private readonly SimulationOptions _simulation;

    public NotificationsController(NotificationDbContext db, SimulationOptions simulation)
    {
        _db = db;
        _simulation = simulation;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationLog>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<NotificationLog>> SendSync(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (_simulation.DelayMs > 0)
        {
            await Task.Delay(_simulation.DelayMs, cancellationToken);
        }

        if (_simulation.SimulateFailure)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                "Симульований збій NotificationService (для тестування трасування помилок).");
        }

        var log = new NotificationLog
        {
            TaskId = request.TaskId,
            Recipient = request.Recipient,
            EventType = request.EventType,
            Message = request.Message,
            DeliveryMode = CommunicationMode.Classic,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(log);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = log.Id }, log);
    }

    [HttpGet("simulation")]
    public ActionResult<SimulationOptions> GetSimulation() => Ok(_simulation);

    [HttpPut("simulation")]
    public ActionResult<SimulationOptions> UpdateSimulation([FromBody] SimulationOptions updated)
    {
        _simulation.DelayMs = Math.Clamp(updated.DelayMs, 0, 10_000);
        _simulation.SimulateFailure = updated.SimulateFailure;
        return Ok(_simulation);
    }
}
