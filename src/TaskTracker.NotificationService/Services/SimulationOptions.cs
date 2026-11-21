namespace TaskTracker.NotificationService.Services;

/// <summary>
/// Налаштування симуляції затримки та збою NotificationService.
/// Використовується в ЛР №3 для наочного порівняння затримок (Classic vs Event-Driven)
/// та демонстрації сценарію з помилкою у Jaeger.
/// </summary>
public class SimulationOptions
{
    /// <summary>Штучна затримка обробки сповіщення (імітація зовнішнього Email/SMTP провайдера), мс.</summary>
    public int DelayMs { get; set; } = 500;

    /// <summary>Якщо true — сервіс генерує помилку обробки для демонстрації збою у трасуванні.</summary>
    public bool SimulateFailure { get; set; } = false;
}
