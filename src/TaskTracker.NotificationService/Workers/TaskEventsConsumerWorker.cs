namespace TaskTracker.NotificationService.Workers;

/// <summary>
/// Заготовка фонового воркера, який підписуватиметься на події task.# у RabbitMQ
/// та асинхронно створюватиме сповіщення (Event-Driven режим).
/// </summary>
public class TaskEventsConsumerWorker : BackgroundService
{
    private readonly ILogger<TaskEventsConsumerWorker> _logger;

    public TaskEventsConsumerWorker(ILogger<TaskEventsConsumerWorker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskEventsConsumerWorker ініціалізовано (заготовка; підписка на RabbitMQ буде додана на наступному етапі).");
        return Task.CompletedTask;
    }
}
