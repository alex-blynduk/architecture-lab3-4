using System.Diagnostics;

namespace TaskTracker.Shared.Telemetry;

/// <summary>
/// Спільні константи та ActivitySource для розподіленого трасування OpenTelemetry
/// при обміні повідомленнями через RabbitMQ.
/// </summary>
public static class TelemetryConstants
{
    public const string ActivitySourceName = "TaskTracker.Messaging";

    public static readonly ActivitySource MessagingActivitySource = new(ActivitySourceName);
}
