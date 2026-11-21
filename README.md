# TaskTracker — Мікросервісна архітектура та Docker (АППЗ, ЛР №3–4)

Навчальний проєкт із дисципліни **«Архітектура та проєктування програмного забезпечення»** (варіант 1 — *Програмне забезпечення для відстеження виконання завдань*).

Проєкт поєднує вимоги **Лабораторної роботи №3** (мікросервіси на .NET 8, порівняння Classic REST та Event-Driven через RabbitMQ, розподілене трасування OpenTelemetry + Jaeger, Web UI) та **Лабораторної роботи №4** (контейнеризація через Dockerfile, оптимізація multi-stage build, оркестрація через Docker Compose).

---

## Архітектура та структура проєкту

```text
architecture-lab3-4/
├── TaskTracker.sln                           # Загальне рішення (.NET 8)
├── README.md                                 # Опис проєкту та інструкція запуску з нуля
└── src/
    ├── TaskTracker.Shared/                   # Спільна бібліотека: доменні події, константи RabbitMQ, DTO
    │   ├── Contracts/                        # CommunicationMode (Classic/EventDriven), SendNotificationRequest
    │   ├── Events/                           # IEvent, TaskCreated, TaskStatusChanged, TaskDeleted
    │   └── Messaging/                        # RabbitMqConstants (exchange "task_events", routing keys)
    │
    ├── TaskTracker.TaskService/              # Мікросервіс №1: управління завданнями + Web UI (порт 5001)
    │   ├── Controllers/                      # TasksController (REST API)
    │   ├── Data/                             # TaskDbContext (власна БД SQLite: tasks.db)
    │   ├── Domain/                           # TaskItem, TaskItemStatus, TaskPriority
    │   ├── Dto/                              # CreateTaskRequest, ChangeTaskStatusRequest, TaskResponse
    │   ├── Services/                         # Інтерфейси ITaskAppService, IEventPublisher, INotificationClient
    │   └── wwwroot/                          # Легкий клієнтський Web UI (HTML/CSS/JS)
    │
    └── TaskTracker.NotificationService/      # Мікросервіс №2: сповіщення REST + RabbitMQ (порт 5002)
        ├── Controllers/                      # NotificationsController (синхронний REST + керування симуляцією)
        ├── Data/                             # NotificationDbContext (власна БД SQLite: notifications.db)
        ├── Domain/                           # NotificationLog
        ├── Services/                         # INotificationProcessor, SimulationOptions (затримка/збій)
        └── Workers/                          # TaskEventsConsumerWorker (фонова підписка на RabbitMQ)
```

### Контейнери (мінімальний набір під 8 ГБ ОЗП)
1. **`task-service`** — REST API завдань + власна SQLite БД + вбудований Web UI (~50–60 МБ ОЗП).
2. **`notification-service`** — приймає сповіщення синхронно (REST) і асинхронно (RabbitMQ) + власна SQLite БД (~50–60 МБ ОЗП).
3. **`rabbitmq`** — брокер повідомлень `rabbitmq:3-management-alpine` (~130–160 МБ ОЗП).
4. **`jaeger`** *(опційний профіль `tracing` для ЛР №3)* — візуалізація трасувань OpenTelemetry (~50 МБ ОЗП).

---

## Запуск проєкту з нуля

### Варіант 1. Локальний запуск через .NET SDK (без Docker)

Підходить, щоб перевірити поточну базову заготовку (`init_structure`) прямо зараз.

1. Переконайся, що встановлено **.NET 8 SDK**:
   ```bash
   dotnet --version
   ```
2. Збери все рішення з кореня репозиторію:
   ```bash
   dotnet build TaskTracker.sln
   ```
3. Запусти два сервіси у двох окремих терміналах:
   ```bash
   # Термінал 1 — TaskService (http://localhost:5001)
   dotnet run --project src/TaskTracker.TaskService

   # Термінал 2 — NotificationService (http://localhost:5002)
   dotnet run --project src/TaskTracker.NotificationService
   ```
4. Відкрий у браузері:
   * **Web UI (заготовка):** `http://localhost:5001`
   * **TaskService Swagger:** `http://localhost:5001/swagger`
   * **NotificationService Swagger:** `http://localhost:5002/swagger`

---

### Варіант 2. Запуск через Docker (інструкція з нуля для початківця)

> **Примітка щодо поточного етапу:** файли `Dockerfile` та `docker-compose.yml` будуть додані на наступному етапі розробки. Нижче наведено повну інструкцію підготовки Docker на ПК з 8 ГБ ОЗП, щоб ти міг заздалегідь встановити все необхідне.

#### Крок 1. Встановлення Docker Desktop (Windows)
1. Завантаж **Docker Desktop for Windows** з офіційного сайту: https://www.docker.com/products/docker-desktop/
2. Під час встановлення залиш увімкненою опцію **Use WSL 2 instead of Hyper-V** (рекомендовано).
3. Після встановлення перезавантаж ПК і запусти **Docker Desktop** (потрібно дочекатися, поки іконка кита внизу стане зеленою — статус *Engine running*).
4. Перевір у PowerShell або cmd, що Docker працює:
   ```bash
   docker --version
   docker compose version
   ```

#### Крок 2. Обмеження пам’яті Docker для ПК з 8 ГБ ОЗП (дуже бажано!)
На Windows Docker працює через WSL 2 і за замовчуванням може забирати до 4 ГБ ОЗП. Щоб система не гальмувала, обмеж його до **2 ГБ**:
1. Відкрий у Провіднику папку `%UserProfile%` (наприклад, `C:\Users\<ТвоєІмя>`).
2. Створи там файл з назвою `.wslconfig` (без розширення `.txt`) із таким вмістом:
   ```ini
   [wsl2]
   memory=2GB
   processors=2
   swap=1GB
   ```
3. Перезапусти WSL у PowerShell:
   ```bash
   wsl --shutdown
   ```
   Після цього знову запусти Docker Desktop.

#### Крок 3. Основні команди Docker (коли додамо `Dockerfile` і `docker-compose.yml`)
Усі команди виконуються з кореневої папки проєкту (там, де лежить `docker-compose.yml`):

* **Запустити 3 основні контейнери (`task-service`, `notification-service`, `rabbitmq`):**
  ```bash
  docker compose up --build
  ```
  *(Додай `-d` у кінці, щоб запустити у фоновому режимі: `docker compose up --build -d`.)*

* **Запустити разом із Jaeger (для трасування в ЛР №3):**
  ```bash
  docker compose --profile tracing up --build
  ```

* **Подивитися список запущених контейнерів (потрібно для звіту ЛР №4):**
  ```bash
  docker ps
  ```

* **Подивитися логи взаємодії сервісів (потрібно для звіту ЛР №4):**
  ```bash
  docker compose logs -f
  ```

* **Подивитися розмір зібраних образів (для порівняння до/після оптимізації в ЛР №4):**
  ```bash
  docker images
  ```

* **Зупинити й прибрати контейнери (щоб звільнити оперативну пам’ять):**
  ```bash
  docker compose --profile tracing down
  ```

#### Корисні адреси після запуску через Docker Compose
| Сервіс | Адреса | Призначення |
| :--- | :--- | :--- |
| **TaskTracker Web UI** | `http://localhost:5001` | Клієнтський інтерфейс користувача |
| **TaskService Swagger** | `http://localhost:5001/swagger` | REST API завдань |
| **NotificationService Swagger** | `http://localhost:5002/swagger` | REST API сповіщень і симуляції збоїв |
| **RabbitMQ Management UI** | `http://localhost:15672` | Веб-панель брокера (логін/пароль: `guest` / `guest`) |
| **Jaeger UI** *(з профілем `tracing`)* | `http://localhost:16686` | Візуалізація розподілених трасувань OpenTelemetry |

---

## Поточний статус (гілка `develop/init_structure`)
- [x] Базова структура рішення `TaskTracker.sln` із трьох проєктів (`Shared`, `TaskService`, `NotificationService`).
- [x] Доменні моделі, події (сумісні з ЛР №2), DTO, інтерфейси та EF Core `DbContext` (SQLite).
- [x] Заготовки контролерів, фонового воркера та Web UI.
- [x] Інструкція запуску з нуля у `README.md`.
- [ ] Повна реалізація Classic REST vs Event-Driven (RabbitMQ) взаємодії.
- [ ] Підключення OpenTelemetry + Jaeger (наскрізне трасування через HTTP і RabbitMQ).
- [ ] Повноцінний інтерактивний Web UI.
- [ ] `Dockerfile` (unoptimized + multi-stage optimized) та `docker-compose.yml`.