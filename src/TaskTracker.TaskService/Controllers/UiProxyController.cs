using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace TaskTracker.TaskService.Controllers;

/// <summary>
/// Легкий проксі-контролер для Web UI (віддається з TaskService), щоб браузер
/// міг читати сповіщення, стан здоров'я та керувати налаштуваннями симуляції
/// NotificationService без проблем із CORS або внутрішніми іменами Docker-мережі.
/// </summary>
[ApiController]
[Route("api/ui")]
public class UiProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public UiProxyController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetServicesStatus(CancellationToken cancellationToken)
    {
        var notificationHealthy = false;
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationProxy");
            using var response = await client.GetAsync("health", cancellationToken);
            notificationHealthy = response.IsSuccessStatusCode;
        }
        catch
        {
            notificationHealthy = false;
        }

        return Ok(new
        {
            taskService = "Healthy",
            notificationService = notificationHealthy ? "Healthy" : "Unavailable"
        });
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationProxy");
            using var response = await client.GetAsync("api/notifications", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = $"NotificationService недоступний: {ex.Message}" });
        }
    }

    [HttpDelete("notifications")]
    public async Task<IActionResult> ClearNotifications(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationProxy");
            using var response = await client.DeleteAsync("api/notifications", cancellationToken);
            return StatusCode((int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = $"NotificationService недоступний: {ex.Message}" });
        }
    }

    [HttpGet("simulation")]
    public async Task<IActionResult> GetSimulation(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationProxy");
            using var response = await client.GetAsync("api/notifications/simulation", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = $"NotificationService недоступний: {ex.Message}" });
        }
    }

    [HttpPut("simulation")]
    public async Task<IActionResult> UpdateSimulation(CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);

            var client = _httpClientFactory.CreateClient("NotificationProxy");
            using var content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            using var response = await client.PutAsync("api/notifications/simulation", content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = responseBody,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = $"NotificationService недоступний: {ex.Message}" });
        }
    }
}
