# TaskTracker — Мікросервісна архітектура та Docker (АППЗ, ЛР №3–4)

Навчальний проєкт із дисципліни **«Архітектура та проєктування програмного забезпечення»** (варіант 1 — *Програмне забезпечення для відстеження виконання завдань*).

Проєкт об’єднує вимоги:
* **ЛР №3:** мікросервісна архітектура на C# (.NET 8), ізольовані БД (*Database-per-Service* на SQLite), порівняння **Classic (REST)** vs **Event-Driven (RabbitMQ)** на одній бізнес-функції, наскрізне розподілене трасування **OpenTelemetry + Jaeger** (включно зі сценарієм збою), інтерактивний **Web UI**.
* **ЛР №4:** контейнеризація мікросервісів через `Dockerfile` із детальними коментарями, порівняння розмірів образу **до** (single-stage SDK) та **після** оптимізації (multi-stage Alpine), запуск усього стеку через `docker-compose.yml`.

---

## Архітектура та структура проєкту

```text
architecture-lab3-4/
├── TaskTracker.sln                               # Загальне рішення (.NET 8)
├── docker-compose.yml                            # Запуск 3 основних контейнерів (+ опційний Jaeger)
├── .dockerignore                                 # Виключення зайвих файлів із контексту збірки Docker
├── README.md                                     # Інструкція запуску з нуля та сценарії перевірки
└── src/
    ├── TaskTracker.Shared/                       # Спільна бібліотека: події, константи RabbitMQ, OpenTelemetry
    │   ├── Contracts/                            # CommunicationMode, SendNotificationRequest
    │   ├── Events/                               # IEvent, TaskCreated, TaskStatusChanged, TaskDeleted
    │   ├── Messaging/                            # RabbitMqConstants (exchange "task_events", routing keys)
    │   └── Telemetry/                            # TelemetryConstants (ActivitySource для RabbitMQ)
    │
    ├── TaskTracker.TaskService/                  # Мікросервіс №1: завдання + Web UI (порт 5001)
    │   ├── Dockerfile                            # Оптимізований multi-stage (sdk:8.0 -> aspnet:8.0-alpine)
    │   ├── Dockerfile.unoptimized                # Неоптимізований single-stage (sdk:8.0) для ЛР №4
    │   ├── Controllers/                          # TasksController, UiProxyController
    │   ├── Data/                                 # TaskDbContext (власна БД SQLite: tasks.db)
    │   ├── Domain/                               # TaskItem, TaskItemStatus, TaskPriority
    │   ├── Services/                             # TaskAppService, HttpNotificationClient, RabbitMqEventPublisher
    │   └── wwwroot/index.html                    # Інтерактивний клієнтський Web UI
    │
    └── TaskTracker.NotificationService/          # Мікросервіс №2: сповіщення REST + RabbitMQ (порт 5002)
        ├── Dockerfile                            # Оптимізований multi-stage (sdk:8.0 -> aspnet:8.0-alpine)
        ├── Controllers/                          # NotificationsController (REST + налаштування симуляції)
        ├── Data/                                 # NotificationDbContext (власна БД SQLite: notifications.db)
        ├── Domain/                               # NotificationLog
        ├── Services/                             # NotificationProcessor, SimulationOptions
        └── Workers/                              # TaskEventsConsumerWorker (підписка на task.# у RabbitMQ)
```

### Контейнери (оптимізовано під 8 ГБ ОЗП)
1. **`task-service`** — REST API завдань + власна БД SQLite + вбудований Web UI (ліміт `192 МБ`).
2. **`notification-service`** — приймає сповіщення синхронно по REST та асинхронно з RabbitMQ + власна БД SQLite (ліміт `192 МБ`).
3. **`rabbitmq`** — брокер повідомлень `rabbitmq:3-management-alpine` (ліміт `384 МБ`).
4. **`jaeger`** *(запускається опційно через `--profile tracing` для ЛР №3)* — візуалізація трасувань OpenTelemetry (ліміт `192 МБ`).

---

## Запуск проєкту з нуля через Docker (інструкція для початківця)

### Крок 1. Встановлення Docker Desktop (Windows)
1. Завантаж **Docker Desktop for Windows** із офіційного сайту: https://www.docker.com/products/docker-desktop/
2. Запусти інсталятор і залиш увімкненою опцію **Use WSL 2 instead of Hyper-V**.
3. Перезавантаж комп’ютер та відкрий **Docker Desktop**. Дочекайся, поки індикатор у лівому нижньому кутку стане зеленим (*Engine running*).
4. Відкрий термінал (PowerShell або cmd) і перевір встановлення:
   ```bash
   docker --version
   docker compose version
   ```

### Крок 2. Обмеження пам’яті Docker (важливо для 8 ГБ ОЗП)
Щоб Docker Desktop через WSL 2 не займав половину оперативної пам’яті:
1. Відкрий папку `%UserProfile%` (наприклад, `C:\Users\<ТвоєІмя>`).
2. Створи файл `.wslconfig` (без розширення `.txt`) із вмістом:
   ```ini
   [wsl2]
   memory=2GB
   processors=2
   swap=1GB
   ```
3. У PowerShell виконай `wsl --shutdown` і знову запусти Docker Desktop.

### Крок 3. Клонування репозиторію та запуск
Перейди в кореневу папку проєкту (там, де знаходиться `docker-compose.yml`):

* **Режим ЛР №4 (рівно 3 контейнери: `task-service`, `notification-service`, `rabbitmq`):**
  ```bash
  docker compose up --build -d
  ```

* **Режим ЛР №3 (додається 4-й легкий контейнер `jaeger` для перегляду трасувань):**
  ```bash
  docker compose --profile tracing up --build -d
  ```

> Перший запуск триватиме 1–3 хвилини, бо Docker завантажить базові образи (`.NET`, `RabbitMQ`, `Jaeger`) та скомпілює C#-сервіси. Усі наступні запуски відбуватимуться за кілька секунд.

### Крок 4. Відкрий у браузері
| Компонент | Адреса | Призначення |
| :--- | :--- | :--- |
| **TaskTracker Web UI** | http://localhost:5001 | Основний інтерфейс користувача |
| **TaskService Swagger** | http://localhost:5001/swagger | REST API мікросервісу завдань |
| **NotificationService Swagger** | http://localhost:5002/swagger | REST API сповіщень та симуляції збоїв |
| **RabbitMQ Management UI** | http://localhost:15672 | Веб-панель брокера (логін/пароль: `guest` / `guest`) |
| **Jaeger UI** *(з `--profile tracing`)* | http://localhost:16686 | Візуалізація розподілених трасувань |

### Корисні команди керування Docker
```bash
# Перевірити стан запущених контейнерів (потрібно для звіту ЛР №4)
docker ps

# Переглянути логи взаємодії сервісів і RabbitMQ (потрібно для звіту ЛР №4)
docker compose logs -f task-service notification-service

# Зупинити всі контейнери та звільнити оперативну пам'ять
docker compose --profile tracing down
```

---

## Як пройти сценарії для звіту ЛР №3

Запусти стек із трасуванням: `docker compose --profile tracing up --build -d` і відкрий http://localhost:5001.

1. **Сценарій 1 — Класичний підхід (Classic REST):**
   * У верхній панелі вибери режим **`Classic (REST)`**, затримку залиш **`500 мс`**, збій вимкнено.
   * Створи завдання — зверни увагу, що час відповіді у таблиці «Порівняння затримок» становитиме **~520–560 мс**, бо `TaskService` чекає завершення HTTP-запиту до `NotificationService`.
   * Відкрий **Jaeger UI** (http://localhost:16686) → Service: `TaskService` → **Find Traces** → побачиш синхронний ланцюжок `POST /api/tasks` → `HTTP POST` → `NotificationService`.

2. **Сценарій 2 — Подієво-орієнтований підхід (Event-Driven / RabbitMQ):**
   * Перемкни режим на **`Event-Driven (RabbitMQ)`** (затримка все ще `500 мс`).
   * Створи ще одне завдання — відповідь прийде за **~15–30 мс**! А за пів секунди сповіщення з’явиться у правій колонці з бейджем `EventDriven (RabbitMQ)`.
   * У **Jaeger UI** відкрий цей trace: побачиш, що спан `POST /api/tasks` → `rabbitmq publish task.created` завершився миттєво, а спан `NotificationService: rabbitmq consume TaskCreated` відпрацював асинхронно у тому ж самому трасуванні!
   * У **RabbitMQ UI** (http://localhost:15672) на вкладці **Exchanges** (`task_events`) та **Queues** (`task_events.notifications`) видно маршрутизацію подій за маскою `task.#`.

3. **Сценарій 3 — Збій одного з сервісів (Error Tracing):**
   * Увімкни прапорець **«Симулювати збій (500 / Exception)»** у верхній панелі Web UI:
     * У режимі **`Classic (REST)`** створення завдання завершиться помилкою `502 Bad Gateway`, транзакція відкотиться, а в Jaeger з’являться червоні error-спани на обох сервісах.
     * У режимі **`Event-Driven (RabbitMQ)`** завдання **успішно створиться** (`201 Created`), а помилка буде ізольована всередині спану `NotificationService: rabbitmq consume` в Jaeger.
   * Додатково можна перевірити реальне падіння контейнера:
     ```bash
     docker stop tasktracker-notification-service
     # Створи завдання у Classic (помилка 502) та в Event-Driven (успіх 201 — подія чекає в черзі RabbitMQ!)
     docker start tasktracker-notification-service
     # Після запуску NotificationService автоматично вичитає накопичену подію з черги RabbitMQ
     ```

---

## Як порівняти розмір Docker-образу для звіту ЛР №4

У проєкті є два Dockerfile для `TaskService`:
* **До оптимізації** (одностадійний на базі `sdk:8.0`): `src/TaskTracker.TaskService/Dockerfile.unoptimized`
* **Після оптимізації** (multi-stage `sdk:8.0` → `aspnet:8.0-alpine`): `src/TaskTracker.TaskService/Dockerfile`

Виконай у корені проєкту:
```bash
# 1. Зібрати неоптимізований образ
docker build -f src/TaskTracker.TaskService/Dockerfile.unoptimized -t tasktracker-task-service:unoptimized .

# 2. Зібрати оптимізований multi-stage образ
docker build -f src/TaskTracker.TaskService/Dockerfile -t tasktracker-task-service:optimized .

# 3. Порівняти розміри обох образів
docker images tasktracker-task-service
```

---

## Локальний запуск без Docker (через .NET 8 SDK)

```bash
dotnet build TaskTracker.sln

# У двох окремих терміналах:
dotnet run --project src/TaskTracker.TaskService
dotnet run --project src/TaskTracker.NotificationService
```
*(Для режиму `EventDriven` у локальному запуску має працювати локальний RabbitMQ на `localhost:5672`.)*