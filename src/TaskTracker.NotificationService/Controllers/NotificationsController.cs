using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskTracker.NotificationService.Data;
using TaskTracker.NotificationService.Domain;
using TaskTracker.NotificationService.Services;
using TaskTracker.Shared.Contracts;

namespace TaskTracker.NotificationService.Controllers;

/// <summary>
/// REST API контролер мікросервісу сповіщень.
/// - POST /api/notifications — синхронний прийом сповіщень (Classic режим)
/// - GET /api/notifications — перегляд історії збережених сповіщень
/// - GET/PUT /api/notifications/simulation — налаштування затримки та збою для ЛР №3
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationDbContext _db;
    private readonly INotificationProcessor _processor;
    private readonly SimulationOptions _simulation;

    public NotificationsController(
        NotificationDbContext db,
        INotificationProcessor processor,
        SimulationOptions simulation)
    {
        _db = db;
        _processor = processor;
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

    [HttpDelete]
    public async Task<IActionResult> ClearAll(CancellationToken cancellationToken)
    {
        var all = await _db.Notifications.ToListAsync(cancellationToken);
        _db.Notifications.RemoveRange(all);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<NotificationLog>> SendSync(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = await _processor.ProcessAsync(
                request.TaskId,
                request.Recipient,
                request.EventType,
                request.Message,
                CommunicationMode.Classic,
                cancellationToken);

            return CreatedAtAction(nameof(GetAll), new { id = log.Id }, log);
        }
        catch (InvalidOperationException ex)
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
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
