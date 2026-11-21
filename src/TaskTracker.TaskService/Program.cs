using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TaskTracker.Shared.Telemetry;
using TaskTracker.TaskService.Data;
using TaskTracker.TaskService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=data/tasks.db";

EnsureSqliteDirectoryExists(connectionString);

builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseSqlite(connectionString));

var notificationServiceUrl = builder.Configuration["Services:NotificationServiceUrl"]
    ?? "http://localhost:5002";

// Типізований HttpClient для синхронного Classic REST виклику (інструментується OpenTelemetry)
builder.Services.AddHttpClient<INotificationClient, HttpNotificationClient>(client =>
{
    client.BaseAddress = new Uri(notificationServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Окремий HttpClient для службових проксі-запитів Web UI
builder.Services.AddHttpClient("NotificationProxy", client =>
{
    client.BaseAddress = new Uri(notificationServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(3);
});

builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddScoped<ITaskAppService, TaskAppService>();

// Підключення OpenTelemetry з експортом трасувань у Jaeger через OTLP (gRPC, порт 4317)
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("TaskService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            // Фільтруємо службові ендпоінти та UI-проксі, щоб у Jaeger були чисті бізнес-траси
            options.Filter = ctx =>
                !ctx.Request.Path.StartsWithSegments("/health") &&
                !ctx.Request.Path.StartsWithSegments("/swagger") &&
                !ctx.Request.Path.StartsWithSegments("/api/ui") &&
                ctx.Request.Path != "/" &&
                !ctx.Request.Path.StartsWithSegments("/index.html");
        })
        .AddHttpClientInstrumentation(options =>
        {
            // Фільтруємо фонові опитування UI-проксі до NotificationService
            options.FilterHttpRequestMessage = req =>
                req.RequestUri is null ||
                (req.Method != HttpMethod.Get && !req.RequestUri.AbsolutePath.EndsWith("/simulation"));
        })
        .AddSource(TelemetryConstants.ActivitySourceName)
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { service = "TaskService", status = "Healthy" }));

app.Run();

static void EnsureSqliteDirectoryExists(string connString)
{
    const string prefix = "Data Source=";
    var idx = connString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (idx < 0) return;

    var path = connString[(idx + prefix.Length)..].Split(';')[0].Trim();
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
    {
        Directory.CreateDirectory(dir);
    }
}
