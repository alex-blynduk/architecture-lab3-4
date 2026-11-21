# ==============================================================================
# Оптимізований багатоступеневий (multi-stage) Dockerfile для NotificationService
# Стадія 1 (build): збірка у .NET 8 SDK
# Стадія 2 (runtime): запуск у мінімальному образі ASP.NET Core 8 Alpine
# ==============================================================================

# --- Стадія 1: Збірка (Build) ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Робоча директорія для збірки
WORKDIR /src

# Копіюємо файли проєктів для ефективного кешування NuGet restore
COPY src/TaskTracker.Shared/TaskTracker.Shared.csproj src/TaskTracker.Shared/
COPY src/TaskTracker.NotificationService/TaskTracker.NotificationService.csproj src/TaskTracker.NotificationService/

# Відновлюємо NuGet-пакети
RUN dotnet restore src/TaskTracker.NotificationService/TaskTracker.NotificationService.csproj

# Копіюємо вихідний код
COPY src/TaskTracker.Shared/ src/TaskTracker.Shared/
COPY src/TaskTracker.NotificationService/ src/TaskTracker.NotificationService/

# Збираємо релізну версію застосунку
RUN dotnet publish src/TaskTracker.NotificationService/TaskTracker.NotificationService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- Стадія 2: Виконання (Runtime) ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Робоча директорія у фінальному образі
WORKDIR /app

# Каталог для власної бази даних SQLite (Database-per-Service)
RUN mkdir -p /app/data

# Копіюємо зібрані файли зі стадії build
COPY --from=build /app/publish .

# Внутрішній порт сервісу
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Запуск мікросервісу NotificationService
ENTRYPOINT ["dotnet", "TaskTracker.NotificationService.dll"]
