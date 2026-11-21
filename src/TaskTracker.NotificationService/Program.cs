using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TaskTracker.NotificationService.Data;
using TaskTracker.NotificationService.Services;
using TaskTracker.NotificationService.Workers;
using TaskTracker.Shared.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Дозволяємо CORS для прямих запитів з Web UI або Swagger
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=data/notifications.db";

EnsureSqliteDirectoryExists(connectionString);

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<SimulationOptions>();
builder.Services.AddScoped<INotificationProcessor, NotificationProcessor>();
builder.Services.AddHostedService<TaskEventsConsumerWorker>();

// Підключення OpenTelemetry з експортом трасувань у Jaeger через OTLP (gRPC, порт 4317)
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("NotificationService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            // Фільтруємо службові та опитувальні GET-запити, щоб у Jaeger були чисті бізнес-траси
            options.Filter = ctx =>
                !ctx.Request.Path.StartsWithSegments("/health") &&
                !ctx.Request.Path.StartsWithSegments("/swagger") &&
                !(ctx.Request.Method == HttpMethods.Get && ctx.Request.Path.StartsWithSegments("/api/notifications"));
        })
        .AddHttpClientInstrumentation()
        .AddSource(TelemetryConstants.ActivitySourceName)
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { service = "NotificationService", status = "Healthy" }));

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
