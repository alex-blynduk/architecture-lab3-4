using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using TaskTracker.Shared.Events;
using TaskTracker.Shared.Messaging;
using TaskTracker.Shared.Telemetry;

namespace TaskTracker.TaskService.Services;

/// <summary>
/// Видавець доменних подій у RabbitMQ (topic exchange "task_events", Event-Driven підхід).
/// Підтримує ліниве підключення / перепідключення та прокидання OpenTelemetry trace-контексту
/// через заголовки AMQP-повідомлення для наскрізного трасування в Jaeger.
/// </summary>
public class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly object _lock = new();

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
    {
        _logger = logger;
        _factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = configuration.GetValue<int?>("RabbitMQ:Port") ?? 5672,
            UserName = configuration["RabbitMQ:User"] ?? "guest",
            Password = configuration["RabbitMQ:Pass"] ?? "guest"
        };
    }

    public void Publish(IEvent @event)
    {
        var routingKey = @event switch
        {
            TaskCreated => RabbitMqConstants.TaskCreatedRoutingKey,
            TaskStatusChanged => RabbitMqConstants.TaskStatusChangedRoutingKey,
            TaskDeleted => RabbitMqConstants.TaskDeletedRoutingKey,
            _ => "task.unknown"
        };

        var eventType = @event.GetType().Name;

        using var activity = TelemetryConstants.MessagingActivitySource.StartActivity(
            $"rabbitmq publish {routingKey}",
            ActivityKind.Producer);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", RabbitMqConstants.ExchangeName);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("event.type", eventType);
        activity?.SetTag("task.id", @event.TaskId.ToString());

        lock (_lock)
        {
            EnsureConnected();

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event, @event.GetType()));
            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            props.Headers = new Dictionary<string, object>
            {
                [RabbitMqConstants.EventTypeHeader] = eventType
            };

            // Вшиваємо W3C traceparent/tracestate у заголовки RabbitMQ-повідомлення
            var contextToInject = activity?.Context ?? Activity.Current?.Context ?? default;
            if (contextToInject != default)
            {
                Propagator.Inject(
                    new PropagationContext(contextToInject, Baggage.Current),
                    props.Headers,
                    (headers, key, value) => headers[key] = value);
            }

            _channel.BasicPublish(
                exchange: RabbitMqConstants.ExchangeName,
                routingKey: routingKey,
                basicProperties: props,
                body: body);

            _logger.LogInformation(
                "[TaskService -> RabbitMQ] Опубліковано {EventType} -> exchange='{Exchange}', routingKey='{RoutingKey}', TaskId={TaskId}",
                eventType, RabbitMqConstants.ExchangeName, routingKey, @event.TaskId);
        }
    }

    private void EnsureConnected()
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
        {
            return;
        }

        CleanupConnection();

        _connection = _factory.CreateConnection("TaskService.Publisher");
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(
            exchange: RabbitMqConstants.ExchangeName,
            type: ExchangeType.Topic,
            durable: true);

        _logger.LogInformation(
            "[TaskService] Підключено до RabbitMQ ({Host}:{Port}), exchange '{Exchange}' готовий.",
            _factory.HostName, _factory.Port, RabbitMqConstants.ExchangeName);
    }

    private void CleanupConnection()
    {
        try { _channel?.Close(); } catch { /* ignore */ }
        try { _connection?.Close(); } catch { /* ignore */ }
        _channel?.Dispose();
        _connection?.Dispose();
        _channel = null;
        _connection = null;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            CleanupConnection();
        }
    }
}
