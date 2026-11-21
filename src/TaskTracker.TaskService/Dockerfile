# ==============================================================================
# Оптимізований багатоступеневий (multi-stage) Dockerfile для TaskService
# Стадія 1 (build): збірка проєкту у повному образі .NET 8 SDK
# Стадія 2 (runtime): копіювання лише готових бінарників у легкий ASP.NET Alpine образ
# ==============================================================================

# --- Стадія 1: Збірка (Build) ---
# Використовуємо офіційний образ із повним .NET 8 SDK для відновлення залежностей і компіляції
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Встановлюємо робочу директорію всередині образу збірки
WORKDIR /src

# Копіюємо спершу лише .csproj файли, щоб Docker кешував шар відновлення NuGet-пакетів
COPY src/TaskTracker.Shared/TaskTracker.Shared.csproj src/TaskTracker.Shared/
COPY src/TaskTracker.TaskService/TaskTracker.TaskService.csproj src/TaskTracker.TaskService/

# Відновлюємо залежності NuGet (виконується повторно лише при зміні .csproj)
RUN dotnet restore src/TaskTracker.TaskService/TaskTracker.TaskService.csproj

# Копіюємо решту вихідного коду спільної бібліотеки та мікросервісу
COPY src/TaskTracker.Shared/ src/TaskTracker.Shared/
COPY src/TaskTracker.TaskService/ src/TaskTracker.TaskService/

# Компілюємо та публікуємо релізну версію застосунку у каталог /app/publish (без повторного restore)
RUN dotnet publish src/TaskTracker.TaskService/TaskTracker.TaskService.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# --- Стадія 2: Виконання (Runtime) ---
# Беремо мінімальний образ ASP.NET Core Runtime на базі Alpine Linux (без SDK та компілятора)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Встановлюємо робочу директорію застосунку у фінальному контейнері
WORKDIR /app

# Створюємо каталог для локальної БД SQLite (монтується як Docker volume)
RUN mkdir -p /app/data

# Копіюємо лише зібрані артефакти зі стадії build
COPY --from=build /app/publish .

# Вказуємо внутрішній HTTP-порт ASP.NET Core 8 у контейнері
ENV ASPNETCORE_URLS=http://+:8080

# Документуємо порт, на якому контейнер приймає трафік
EXPOSE 8080

# Точка входу: запуск скомпільованого мікросервісу TaskService
ENTRYPOINT ["dotnet", "TaskTracker.TaskService.dll"]
