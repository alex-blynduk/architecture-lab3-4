using Microsoft.EntityFrameworkCore;
using TaskTracker.NotificationService.Data;
using TaskTracker.NotificationService.Services;
using TaskTracker.NotificationService.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=data/notifications.db";

EnsureSqliteDirectoryExists(connectionString);

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<SimulationOptions>();
builder.Services.AddHostedService<TaskEventsConsumerWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.EnsureCreated();
}

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
