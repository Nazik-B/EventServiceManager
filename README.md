# Event Service Manager API

Учебный проект ASP.NET Core Web API для управления событиями (Event). Реализован CRUD, валидация входных данных, фильтрация, пагинация, централизованная обработка ошибок и документация через Swagger.

## Стек технологий

- .NET 10 / ASP.NET Core Web API
- Swashbuckle.AspNetCore (Swagger UI)
- Entity Framework Core
- Npgsql.EntityFrameworkCore.PostgreSQL
- EF Core Migrations (`dotnet-ef`)
- PostgreSQL 16
- xUnit (unit- и интеграционное тестирование)
- Microsoft.EntityFrameworkCore.InMemory для unit-тестов
- Testcontainers for .NET (`Testcontainers.PostgreSql`) для интеграционных тестов

## Требования

- Установленный .NET SDK 10.0 или новее
- PostgreSQL 16 или новее
- Docker Desktop и Docker Compose — рекомендуемый способ запуска PostgreSQL
- Docker Desktop должен быть запущен для интеграционных тестов (см. раздел «Запуск тестов»)
- Инструмент EF Core CLI: `dotnet tool install --global dotnet-ef`
- Git

Перед запуском API убедитесь, что PostgreSQL доступен по параметрам, указанным в строке подключения.

## Запуск проекта

Клонируйте репозиторий и перейдите в ветку `sprint-6`, где ведётся текущая разработка:

```bash
git clone <URL_репозитория>
cd EventServiceManager
git checkout sprint-6
```

### Запуск PostgreSQL через Docker

В корневой папке проекта находится `docker-compose.yml`. Для запуска базы данных событий выполните:

```bash
docker compose up -d events-db
```

Проверьте, что контейнер работает:

```bash
docker compose ps events-db
```

Контейнер `eventapi-events-db` должен иметь состояние `running` или `healthy`. PostgreSQL должен быть доступен на `localhost:5432`.

### Настройка строки подключения

Строка подключения задаётся в файле:

```text
MyWebApiEventSrvManagerProj/appsettings.json
```

Пример конфигурации для локального PostgreSQL-контейнера:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=events;Username=postgres;Password=postgres"
  }
}
```

Значения должны совпадать с параметрами PostgreSQL:

- `Host` — адрес PostgreSQL, для локального Docker-контейнера `localhost`
- `Port` — внешний порт контейнера, обычно `5432`
- `Database` — имя базы данных, например `events`
- `Username` — пользователь PostgreSQL, например `postgres`
- `Password` — пароль пользователя PostgreSQL

### Схема базы данных и миграции

Схема базы данных управляется миграциями EF Core. Файлы миграций лежат в папке `Migrations` проекта `MyWebApiEventSrvManagerProj`, и именно они определяют таблицы `events` и `bookings`, первичные ключи, внешний ключ `bookings.EventId → events.Id` и индекс по `EventId`. `Database.EnsureCreated()` не используется: он несовместим с миграциями.

При старте API в `Program.cs` вызывается `Database.Migrate()`: все неприменённые миграции применяются к базе автоматически. Поэтому для локального запуска достаточно поднять PostgreSQL и выполнить `dotnet run`. Команду `dotnet ef database update` используйте, когда нужно применить миграции вручную, не запуская API.

Установите инструмент EF Core CLI (один раз):

```bash
dotnet tool install --global dotnet-ef
```

Все команды выполняются из корня репозитория. Перед применением миграций запустите PostgreSQL (`docker compose up -d events-db`), а строка подключения `DefaultConnection` в `appsettings.json` должна указывать на эту базу.

Создать новую миграцию после изменения моделей или конфигураций:

```bash
dotnet ef migrations add <MigrationName> --project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj --startup-project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj
```

Применить все неприменённые миграции к базе данных:

```bash
dotnet ef database update --project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj --startup-project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj
```

Посмотреть список миграций:

```bash
dotnet ef migrations list --project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj --startup-project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj
```

Удалить последнюю миграцию, если она ещё не применена к базе:

```bash
dotnet ef migrations remove --project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj --startup-project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj
```

Пересоздать локальную базу с нуля:

```bash
dotnet ef database drop --force --project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj --startup-project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj
dotnet ef database update --project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj --startup-project MyWebApiEventSrvManagerProj/MyWebApiEventSrvManagerProj.csproj
```

Если имя миграции уже занято, выберите другое (например, `AddBookingStatusIndex`) или удалите неприменённую миграцию командой `migrations remove`.

### Запуск API

Перейдите в папку с файлом проекта (`.csproj`) и восстановите зависимости:

```bash
cd MyWebApiEventSrvManagerProj
dotnet restore
```

Запустите приложение:

```bash
dotnet run
```

По умолчанию сервис доступен по адресу `http://localhost:5102`.
Документация Swagger доступна по адресу `http://localhost:5102/swagger`.

## Валидация

- `Title`, `StartAt`, `EndAt` — обязательные поля.
- `Description` — опциональное поле (может быть `null`).
- `EndAt` должен быть строго позже `StartAt`, иначе API вернёт `400 Bad Request` с описанием ошибки в поле `EndAt` ("EndAt must be later than StartAt").
- При создании и обновлении используются отдельные DTO (`CreateEventRequest`, `UpdateEventRequest`), клиент не может передавать `Id` вручную — идентификатор генерируется сервисом автоматически.
- При создании брони (`POST /events/{id}/book`) `BookingService` проверяет существование события через `IEventService`; если событие не найдено, запрос завершается ошибкой `404 Not Found` до создания брони.
- `TotalSeats` — обязательное поле; должно быть больше 0.
- `AvailableSeats` клиент не передаёт: сервис вычисляет его автоматически.

## Модель Event

| Поле | Тип | Описание |
|---|---|---|
| `id` | `Guid` | Уникальный идентификатор события. |
| `title` | `string` | Название события. |
| `description` | `string?` | Необязательное описание. |
| `startAt` | `DateTime` | Время начала события. |
| `endAt` | `DateTime` | Время окончания события. |
| `totalSeats` | `int` | Общая вместимость события. Обязательное значение больше 0. |
| `availableSeats` | `int` | Текущее число свободных мест. При создании равно `totalSeats`; уменьшается после успешной брони и увеличивается при возврате места. |

## Эндпоинты API

### Events

| Метод  | URL              | Описание                          | Успех         | Ошибка              |
|--------|------------------|------------------------------------|---------------|----------------------|
| GET    | /events          | Получить список событий (с фильтрацией и пагинацией) | 200 OK | — |
| GET    | /events/{id}     | Получить событие по id             | 200 OK        | 404 Not Found        |
| POST   | /events          | Создать новое событие              | 201 Created   | 400 Bad Request      |
| PUT    | /events/{id}     | Обновить событие целиком           | 204 No Content| 404 Not Found, 400 Bad Request |
| DELETE | /events/{id}     | Удалить событие                    | 204 No Content| 404 Not Found        |

### Bookings

| Метод  | URL                     | Описание                                   | Успех          | Ошибка         |
|--------|-------------------------|---------------------------------------------|----------------|----------------|
| POST   | /events/{id}/book       | Создать бронь на указанное событие          | 202 Accepted   | 404 Not Found, 409 Conflict   |
| GET    | /bookings/{id}          | Получить текущий статус брони по id         | 200 OK         | 404 Not Found  |

`POST /events/{id}/book` возвращает заголовок `Location`, указывающий на `/bookings/{bookingId}` для отслеживания статуса созданной брони.
`POST /events/{id}/book` возвращает `409 Conflict`, если событие существует, но свободных мест больше нет. Ошибка соответствует `NoAvailableSeatsException`.

## GET /events — параметры фильтрации и пагинации

Эндпоинт поддерживает следующие опциональные query-параметры. Все фильтры применяются совместно (логическое И):

| Параметр | Тип | По умолчанию | Описание |
|---|---|---|---|
| `title` | string | — | Поиск по названию события, регистронезависимый, частичное совпадение |
| `from` | DateTime | — | Возвращает события, которые начинаются не раньше указанной даты (`StartAt >= from`) |
| `to` | DateTime | — | Возвращает события, которые заканчиваются не позже указанной даты (`EndAt <= to`) |
| `page` | int | 1 | Номер страницы результатов |
| `pageSize` | int | 10 | Количество элементов на странице |

### Формат ответа GET /events

Ответ возвращается в виде объекта `PaginatedResult`:

```json
{
  "totalCount": 3,
  "items": [
    {
      "id": 1,
      "title": "Team Meeting",
      "description": null,
      "startAt": "2026-08-01T10:00:00",
      "endAt": "2026-08-01T11:00:00"
    }
  ],
  "page": 1,
  "pageSize": 10
}
```

- `totalCount` — общее количество событий, соответствующих фильтрам (без учёта пагинации)
- `items` — массив событий на текущей странице
- `page` — номер текущей страницы
- `pageSize` — размер страницы

## Модель Booking

| Поле | Тип | Описание |
|---|---|---|
| `id` | `Guid` | Уникальный идентификатор брони. |
| `eventId` | `Guid` | Идентификатор события, к которому относится бронь. |
| `status` | `string` | Текущий статус брони: `Pending`, `Confirmed` или `Rejected`. |
| `createdAt` | `DateTime` | Момент создания брони (UTC). |
| `processedAt` | `DateTime?` | Момент завершения обработки брони фоновым сервисом (UTC). `null`, пока бронь находится в статусе `Pending`. |

**Статусы:**

- **`Pending`** — бронь создана и ожидает обработки в фоновой очереди. Начальный статус для любой новой брони.
- **`Confirmed`** — бронь успешно обработана и подтверждена.
- **`Rejected`** — бронь отклонена во время фоновой обработки. Если для брони ранее резервировалось место, оно освобождается и возвращается в `AvailableSeats`.

## Синхронизация и конкурентность

`AppDbContext` зарегистрирован со временем жизни `scoped`, поэтому каждый HTTP-запрос получает отдельный экземпляр контекста EF Core.

Для предотвращения овербукинга `BookingService` использует статический `SemaphoreSlim`. Он защищает критическую секцию, в которой:

1. Загружается событие.
2. Проверяется наличие свободных мест.
3. Уменьшается `AvailableSeats`.
4. Создаётся новая бронь со статусом `Pending`.
5. Изменения сохраняются одним вызовом `SaveChangesAsync()`.

`SemaphoreSlim` используется вместо `lock`, потому что внутри критической секции выполняются асинхронные операции с `await`.

Фоновый сервис не использует один общий `DbContext`. Он получает `IServiceScopeFactory`, загружает идентификаторы необработанных броней в отдельном scope, а для обработки каждой брони создаёт новый scope. Поэтому каждая фоновая задача использует собственный экземпляр `AppDbContext` и scoped-репозитории.

## Логика фоновой обработки бронирований

Создание брони через `POST /events/{id}/book` не выполняет обработку синхронно:

1. `BookingService` проверяет существование события через `IEventService`. Если событие не найдено, запрос завершается `404` до создания брони.
2. Если событие существует, создаётся объект `Booking` со статусом `Pending`, сохраняется в хранилище и добавляется в очередь на обработку.
3. Контроллер немедленно возвращает `202 Accepted` с телом брони и заголовком `Location` — клиент не ждёт завершения обработки.
4. Фоновый сервис `BookingProcessingBackgroundService` (реализует `BackgroundService`) асинхронно забирает брони из очереди, выполняет проверку бизнес-правил, переводит бронь в `Confirmed` или `Rejected` и заполняет `processedAt`.
5. Клиент узнаёт итоговый результат, опрашивая `GET /bookings/{id}` до тех пор, пока `status` не сменится с `Pending` на конечное значение.

Такой подход разгружает HTTP-запрос от потенциально долгой обработки и позволяет масштабировать очередь бронирований независимо от веб-слоя.

## Примеры запросов (curl)

### Создание события

```bash
curl -X POST http://localhost:5102/events \
  -H "Content-Type: application/json" \
  -d '{"title":"Standup","description":"Daily meeting","startAt":"2026-07-09T10:00:00","endAt":"2026-07-09T10:15:00","totalSeats":10}'
```

### Получение списка событий

```bash
curl http://localhost:5102/events
```

### Получение списка событий с фильтрацией и пагинацией

```bash
# Поиск по названию
curl "http://localhost:5102/events?title=standup"

# Фильтр по диапазону дат
curl "http://localhost:5102/events?from=2026-08-01T00:00:00&to=2026-08-31T23:59:59"

# Пагинация
curl "http://localhost:5102/events?page=2&pageSize=5"

# Комбинированный запрос
curl "http://localhost:5102/events?title=review&from=2026-08-01T00:00:00&to=2026-08-31T23:59:59&page=1&pageSize=10"
```

### Получение события по id

```bash
curl http://localhost:5102/events/EVENT_ID
```

### Обновление события

```bash
curl -X PUT http://localhost:5102/events/EVENT_ID \
  -H "Content-Type: application/json" \
  -d '{"title":"Updated Standup","startAt":"2026-07-09T11:00:00","endAt":"2026-07-09T11:30:00"}'
```

### Удаление события

```bash
curl -X DELETE http://localhost:5102/events/EVENT_ID
```

### Сквозной сценарий бронирования

```bash
# 1. Создать событие
curl -i -X POST http://localhost:5102/events \
  -H "Content-Type: application/json" \
  -d '{"title":"Final Check","startAt":"2026-08-10T10:00:00","endAt":"2026-08-10T11:00:00","totalSeats": 3}'
# → 201 Created, тело содержит "id" события (EVENT_ID)

# 2. Создать бронь на это событие
curl -i -X POST http://localhost:5102/events/EVENT_ID/book
# → 202 Accepted, Location: /bookings/BOOKING_ID, status: "Pending"

# 3. Сразу проверить статус
curl -i http://localhost:5102/bookings/BOOKING_ID
# → 200 OK, status: "Pending", processedAt: null

# 4. Подождать несколько секунд и проверить снова
sleep 10
curl -i http://localhost:5102/bookings/BOOKING_ID
# → 200 OK, status: "Confirmed", processedAt заполнен

# 5. Четвёртая бронь отклоняется

curl -i -X POST http://localhost:5102/events/EVENT_ID/book
# → 202 Accepted

curl -i -X POST http://localhost:5102/events/EVENT_ID/book
# → 202 Accepted

curl -i -X POST http://localhost:5102/events/EVENT_ID/book
# → 409 Conflict
```

Тот же сценарий можно выполнить через Swagger UI (`/swagger`): последовательно вызвать `POST /events`, `POST /events/{id}/book` и несколько раз `GET /bookings/{id}` с задержкой, наблюдая переход статуса из `Pending` в `Confirmed`.

После трёх успешных броней `availableSeats` равен `0`. Четвёртый запрос не создаёт бронь и возвращает `409 Conflict`, что предотвращает овербукинг.

## Формат ответа при ошибках

Все ошибки API возвращаются в едином формате **Problem Details** (RFC 9110), с соответствующим HTTP-статусом:

```json
{
  "type": "[https://tools.ietf.org/html/rfc9110#section-15.5.5](https://tools.ietf.org/html/rfc9110#section-15.5.5)",
  "title": "Resource not found",
  "status": 404,
  "detail": "Event with id 999 was not found",
  "instance": "/events/999",
  "traceId": "0HNNAG8906Q2O:00000001"
}
```

### Соответствие типов ошибок и статус-кодов

| Статус | Когда возвращается |
|---|---|
| 400 Bad Request | Ошибки валидации входных данных (пустой Title, EndAt раньше StartAt, отсутствующие обязательные поля) |
| 404 Not Found | Событие или бронь с указанным ID не найдены (GET, PUT, DELETE, POST /events/{id}/book) |
| 409 Conflict | На событии нет свободных мест при попытке создать бронь (NoAvailableSeatsException) |
| 500 Internal Server Error | Непредвиденные ошибки сервера |

### Поля ответа об ошибке

| Поле | Описание |
|---|---|
| `type` | Ссылка на секцию RFC 9110, описывающую данный класс ошибки |
| `title` | Краткое человекочитаемое описание ошибки |
| `status` | HTTP статус-код |
| `detail` | Детальное сообщение об ошибке |
| `instance` | Путь запроса, вызвавшего ошибку |
| `traceId` | Уникальный идентификатор запроса для трассировки в логах |

Все ошибки логируются через встроенный `ILogger`: клиентские ошибки (4xx) — на уровне `Warning`, непредвиденные серверные ошибки (5xx) — на уровне `Error`.

## Запуск тестов

В решении два тестовых проекта:

- `EventService.Tests` — unit-тесты сервисов на xUnit: `EventService` (успешные и неуспешные сценарии CRUD, фильтрации, пагинации и валидации) и `BookingService` (создание брони с существующим и несуществующим событием). Используют провайдер `Microsoft.EntityFrameworkCore.InMemory`, не требуют PostgreSQL и Docker. Каждый тестовый класс создаёт отдельный `ServiceProvider` и уникальную InMemory-базу через `Guid.NewGuid().ToString()`. В тестах конкурентности каждый параллельный запрос создаёт отдельный DI scope и получает собственный `AppDbContext`, но использует общую InMemory-базу тестового класса.
- `EventApi.IntegrationTests` — интеграционные тесты репозиториев на реальной базе PostgreSQL через Testcontainers.

### Интеграционные тесты и Docker

Интеграционные тесты проверяют все методы `EventRepository` и `BookingRepository` на настоящей базе PostgreSQL 16, включая все фильтры (`title`, `from`, `to`, их комбинации и границы диапазонов) и пагинацию метода `GetPagedAsync`.

**Для запуска интеграционных тестов необходим запущенный Docker** (Docker Desktop со статусом `Engine running`). Вручную поднимать контейнер или запускать `docker compose` не нужно: Testcontainers сам скачает образ `postgres:16-alpine` (при первом запуске это займёт время), запустит контейнер и удалит его после тестов.

Как устроена изоляция тестов:

- Все тесты используют один контейнер PostgreSQL (xUnit collection fixture `PostgresCollection`).
- Строка подключения берётся из объекта контейнера (порт назначается автоматически), а не из `appsettings.json`.
- Тесты работают с отдельной базой `event_service_tests`.
- Перед каждым тестом база пересоздаётся: `Database.EnsureDeleted()`, затем `Database.Migrate()`. Схема создаётся теми же миграциями EF Core, что и в приложении, поэтому тесты не зависят от порядка запуска.

Если Docker не запущен, интеграционные тесты завершатся ошибкой `DockerUnavailableException`. Запустите Docker Desktop, убедитесь, что команда `docker ps` отвечает, и повторите запуск.

### Команды

Запустить все тесты из корня решения (нужен Docker):

```bash
dotnet test
```

Только unit-тесты (Docker не нужен):

```bash
dotnet test EventService.Tests
```

Только интеграционные тесты (нужен Docker):

```bash
dotnet test EventApi.IntegrationTests
```

Запустить тесты с отчётом о покрытии кода:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Ожидаемый результат при успешном прохождении:

```text
Сводка теста: сбой: 0, пропущено: 0
```