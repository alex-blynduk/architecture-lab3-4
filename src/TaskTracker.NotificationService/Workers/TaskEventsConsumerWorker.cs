using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TaskTracker.NotificationService.Services;
using TaskTracker.Shared.Contracts;
using TaskTracker.Shared.Events;
using TaskTracker.Shared.Messaging;
using TaskTracker.Shared.Telemetry;

namespace TaskTracker.NotificationService.Workers;

/// <summary>
/// Фоновий сервіс (Consumer), який підписується на всі події завдань (task.#)
/// через topic exchange "task_events" у RabbitMQ (Event-Driven режим).
/// Підтримує автоматичне перепідключення та відновлення W3C trace-контексту для Jaeger.
/// </summary>
public class TaskEventsConsumerWorker : BackgroundService
{
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TaskEventsConsumerWorker> _logger;

    public TaskEventsConsumerWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<TaskEventsConsumerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = _configuration["RabbitMQ:Host"] ?? "localhost";
        var port = _configuration.GetValue<int?>("RabbitMQ:Port") ?? 5672;
        var user = _configuration["RabbitMQ:User"] ?? "guest";
        var pass = _configuration["RabbitMQ:Pass"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = user,
            Password = pass,
            DispatchConsumersAsync = true
        };

        // Цикл автоматичного підключення: якщо RabbitMQ ще стартує або тимчасово недоступний,
        // сервіс не падає, а повторює спробу кожні 3 секунди.
        while (!stoppingToken.IsCancellationRequested)
        {
            IConnection? connection = null;
            IModel? channel = null;

            try
            {
                connection = factory.CreateConnection("NotificationService.Consumer");
                channel = connection.CreateModel();

                channel.ExchangeDeclare(
                    exchange: RabbitMqConstants.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true);

                channel.QueueDeclare(
                    queue: RabbitMqConstants.NotificationsQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                channel.QueueBind(
                    queue: RabbitMqConstants.NotificationsQueueName,
                    exchange: RabbitMqConstants.ExchangeName,
                    routingKey: RabbitMqConstants.NotificationsBindingKey);

                channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                var connectionLost = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdown += (_, _) => connectionLost.TrySetResult(true);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, ea) =>
                {
                    await HandleMessageAsync(channel, ea, stoppingToken);
                };

                channel.BasicConsume(
                    queue: RabbitMqConstants.NotificationsQueueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation(
                    "[NotificationService] Підключено до RabbitMQ ({Host}:{Port}). Черга '{Queue}' прив'язана до '{Exchange}' ({Binding}).",
                    host, port, RabbitMqConstants.NotificationsQueueName, RabbitMqConstants.ExchangeName, RabbitMqConstants.NotificationsBindingKey);

                // Очікуємо зупинки застосунку або розриву з'єднання з брокером
                using var reg = stoppingToken.Register(() => connectionLost.TrySetResult(false));
                await connectionLost.Task;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "[NotificationService] Не вдалося підключитися до RabbitMQ ({Host}:{Port}): {Message}. Повтор через 3 с...",
                    host, port, ex.Message);
            }
            finally
            {
                try { channel?.Close(); } catch { /* ignore */ }
                try { connection?.Close(); } catch { /* ignore */ }
                channel?.Dispose();
                connection?.Dispose();
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(IModel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var eventType = ReadHeader(ea.BasicProperties?.Headers, RabbitMqConstants.EventTypeHeader) ?? "UnknownEvent";
        var parentContext = Propagator.Extract(default, ea.BasicProperties?.Headers, ExtractTraceContextFromHeaders);
        Baggage.Current = parentContext.Baggage;

        using var activity = TelemetryConstants.MessagingActivitySource.StartActivity(
            $"rabbitmq consume {eventType}",
            ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", RabbitMqConstants.ExchangeName);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", ea.RoutingKey);
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("event.type", eventType);

        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var (taskId, recipient, message) = ParseEvent(eventType, json);

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<INotificationProcessor>();

            await processor.ProcessAsync(
                taskId,
                recipient,
                eventType,
                message,
                CommunicationMode.EventDriven,
                stoppingToken);

            channel.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error", true);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);

            _logger.LogError(ex,
                "[NotificationService] Помилка обробки повідомлення RabbitMQ ({EventType}, routingKey={RoutingKey}).",
                eventType, ea.RoutingKey);

            // Під час навчальної демонстрації помилки відхиляємо без безкінечного requeue
            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private static (Guid TaskId, string Recipient, string Message) ParseEvent(string eventType, string json)
    {
        switch (eventType)
        {
            case nameof(TaskCreated):
                var created = JsonSerializer.Deserialize<TaskCreated>(json)
                    ?? throw new InvalidOperationException("Не вдалося десеріалізувати TaskCreated.");
                return (
                    created.TaskId,
                    created.Assignee,
                    $"Створено нове завдання «{created.Title}» (пріоритет: {created.Priority}).");

            case nameof(TaskStatusChanged):
                var changed = JsonSerializer.Deserialize<TaskStatusChanged>(json)
                    ?? throw new InvalidOperationException("Не вдалося десеріалізувати TaskStatusChanged.");
                return (
                    changed.TaskId,
                    changed.Assignee,
                    $"Статус завдання «{changed.Title}» змінено: {changed.OldStatus} -> {changed.NewStatus}.");

            case nameof(TaskDeleted):
                var deleted = JsonSerializer.Deserialize<TaskDeleted>(json)
                    ?? throw new InvalidOperationException("Не вдалося десеріалізувати TaskDeleted.");
                return (
                    deleted.TaskId,
                    deleted.Assignee,
                    $"Завдання «{deleted.Title}» видалено. Причина: {deleted.Reason}.");

            default:
                return (Guid.Empty, "System", $"Отримано подію {eventType}: {json}");
        }
    }

    private static string? ReadHeader(IDictionary<string, object>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            string s => s,
            _ => raw.ToString()
        };
    }

    private static IEnumerable<string> ExtractTraceContextFromHeaders(IDictionary<string, object>? headers, string key)
    {
        var value = ReadHeader(headers, key);
        return string.IsNullOrEmpty(value) ? Array.Empty<string>() : [value];
    }
}
