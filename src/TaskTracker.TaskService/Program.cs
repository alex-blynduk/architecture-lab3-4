using Microsoft.EntityFrameworkCore;
using TaskTracker.TaskService.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=data/tasks.db";

// Переконуємося, що директорія для файлу SQLite існує перед першим підключенням
EnsureSqliteDirectoryExists(connectionString);

builder.Services.AddDbContext<TaskDbContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

// Створюємо таблиці SQLite при першому запуску (спрощено для навчального мікросервісу)
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
