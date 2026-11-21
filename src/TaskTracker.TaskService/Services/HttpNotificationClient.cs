using System.Diagnostics;
using System.Net.Http.Json;
using TaskTracker.Shared.Contracts;

namespace TaskTracker.TaskService.Services;

/// <summary>
/// Синхронний HTTP/REST-клієнт до мікросервісу NotificationService (Classic підхід).
/// HttpClient автоматично інструментований OpenTelemetry, тому заголовок traceparent
/// передається у NotificationService автоматично.
/// </summary>
public class HttpNotificationClient : INotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpNotificationClient> _logger;

    public HttpNotificationClient(HttpClient httpClient, ILogger<HttpNotificationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendAsync(SendNotificationRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[TaskService -> REST] Синхронний виклик NotificationService для {EventType} (TaskId={TaskId})",
            request.EventType, request.TaskId);

        using var response = await _httpClient.PostAsJsonAsync("api/notifications", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var message = $"NotificationService повернув {(int)response.StatusCode} ({response.StatusCode}): {errorBody}";
            Activity.Current?.SetStatus(ActivityStatusCode.Error, message);
            Activity.Current?.SetTag("error", true);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
    }
}
