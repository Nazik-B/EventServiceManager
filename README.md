# Event Service Manager API

Учебный проект ASP.NET Core Web API для управления событиями (Event). Реализован CRUD, валидация входных данных, фильтрация, пагинация, централизованная обработка ошибок и документация через Swagger. Доступ к API защищён JWT-аутентификацией с ролями `User` и `Admin`.

## Стек технологий

- .NET 10 / ASP.NET Core Web API
- Swashbuckle.AspNetCore (Swagger UI)
- JWT Bearer-аутентификация (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- Entity Framework Core
- Npgsql.EntityFrameworkCore.PostgreSQL
- EF Core Migrations (`dotnet-ef`)
- PostgreSQL 16
- xUnit (unit- и интеграционное тестирование)
- Microsoft.EntityFrameworkCore.InMemory для unit-тестов
- Testcontainers for .NET (`Testcontainers.PostgreSql`) для интеграционных тестов

## Архитектура и структура проекта

Решение построено по слоистой (чистой) архитектуре. Зависимости направлены только внутрь: внешние слои знают о внутренних, но не наоборот.

```text
EventServiceManager/
├── EventsApi.Domain/           # сущности и доменные исключения
├── EventsApi.Application/      # сценарии использования, DTO, интерфейсы репозиториев
├── EventsApi.Infrastructure/   # EF Core, репозитории, миграции, фоновая обработка
├── EventsApi.Presentation/     # веб-слой: контроллеры, middleware, Program.cs
├── EventsApi.UnitTests/
└── EventsApi.IntegrationTests/
```

| Слой | Содержимое | Зависит от |
|---|---|---|
| `EventsApi.Domain` | Сущности `Event`, `Booking`, `BookingStatus`; сущность `User` и роли; исключения `NotFoundException`, `NoAvailableSeatsException`, `DomainValidationException`, `PastEventBookingException`, `ActiveBookingsLimitExceededException` | ни от чего |
| `EventsApi.Application` | DTO; интерфейсы `IEventRepository`, `IBookingRepository`; сервисы `EventService`, `BookingService`, `BookingProcessingService`, `AuthService`; метод `AddApplication()` | Domain |
| `EventsApi.Infrastructure` | `AppDbContext`, конфигурации и конвертеры EF Core; реализации репозиториев; миграции; `BookingProcessingBackgroundService`; методы `AddInfrastructure()` и `ApplyMigrations()` | Application, Domain |
| `EventsApi.Presentation` | Контроллеры, `ExceptionHandlingMiddleware`, `ExceptionMapper`, `Program.cs` (composition root) | Application, Infrastructure |

Правила:

- Application не зависит от Infrastructure: интерфейсы репозиториев лежат в Application, реализации — в Infrastructure.
- Контроллеры только принимают HTTP-запрос, вызывают сервис Application и возвращают ответ, бизнес-логики в них нет.
- Доменные исключения превращаются в HTTP-статусы в `ExceptionMapper` (Presentation): `NotFoundException` → 404, `NoAvailableSeatsException` и `ActiveBookingsLimitExceededException` → 409, ошибки валидации и бронь прошедшего события → 400, доступ к чужой брони → 403.
- `Program.cs` только регистрирует зависимости через `AddApplication()` и `AddInfrastructure(...)`.
- Тестовые проекты ссылаются на Application, Domain и Infrastructure, но не на Presentation.

## Требования

- Установленный .NET SDK 10.0 или новее
- PostgreSQL 16 или новее
- Docker Desktop и Docker Compose — рекомендуемый способ запуска PostgreSQL
- Docker Desktop должен быть запущен для интеграционных тестов (см. раздел «Запуск тестов»)
- Инструмент EF Core CLI: `dotnet tool install --global dotnet-ef`
- Git

Перед запуском API убедитесь, что PostgreSQL доступен по параметрам, указанным в строке подключения.

## Запуск проекта

Клонируйте репозиторий и перейдите в ветку `sprint-8`, где ведётся текущая разработка:

```bash
git clone <URL_репозитория>
cd EventServiceManager
git checkout sprint-8
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
EventsApi.Presentation/appsettings.json
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

Схема базы данных управляется миграциями EF Core. Файлы миграций лежат в папке `Migrations` проекта `EventsApi.Infrastructure`, и именно они определяют таблицы `events` и `bookings`, первичные ключи, внешний ключ `bookings.EventId → events.Id` и индекс по `EventId`. `Database.EnsureCreated()` не используется: он несовместим с миграциями.

При старте API `Program.cs` вызывает `app.Services.ApplyMigrations()` (метод расширения из Infrastructure), который выполняет `Database.Migrate()`: все неприменённые миграции применяются к базе автоматически. Поэтому для локального запуска достаточно поднять PostgreSQL и выполнить `dotnet run`. Команду `dotnet ef database update` используйте, когда нужно применить миграции вручную, не запуская API.

Установите инструмент EF Core CLI (один раз):

```bash
dotnet tool install --global dotnet-ef
```

Все команды выполняются из корня репозитория. Перед применением миграций запустите PostgreSQL (`docker compose up -d events-db`), а строка подключения `DefaultConnection` в `appsettings.json` должна указывать на эту базу.

Создать новую миграцию после изменения моделей или конфигураций:

```bash
dotnet ef migrations add <MigrationName> --project EventsApi.Infrastructure/EventsApi.Infrastructure.csproj --startup-project EventsApi.Presentation/EventsApi.Presentation.csproj
```

Применить все неприменённые миграции к базе данных:

```bash
dotnet ef database update --project EventsApi.Infrastructure/EventsApi.Infrastructure.csproj --startup-project EventsApi.Presentation/EventsApi.Presentation.csproj
```

Посмотреть список миграций:

```bash
dotnet ef migrations list --project EventsApi.Infrastructure/EventsApi.Infrastructure.csproj --startup-project EventsApi.Presentation/EventsApi.Presentation.csproj
```

Удалить последнюю миграцию, если она ещё не применена к базе:

```bash
dotnet ef migrations remove --project EventsApi.Infrastructure/EventsApi.Infrastructure.csproj --startup-project EventsApi.Presentation/EventsApi.Presentation.csproj
```

Пересоздать локальную базу с нуля:

```bash
dotnet ef database drop --force --project EventsApi.Infrastructure/EventsApi.Infrastructure.csproj --startup-project EventsApi.Presentation/EventsApi.Presentation.csproj
dotnet ef database update --project EventsApi.Infrastructure/EventsApi.Infrastructure.csproj --startup-project EventsApi.Presentation/EventsApi.Presentation.csproj
```

Если имя миграции уже занято, выберите другое (например, `AddBookingStatusIndex`) или удалите неприменённую миграцию командой `migrations remove`.

### Запуск API

Запустите приложение из корня решения:

```bash
dotnet run --project EventsApi.Presentation
```

Либо перейдите в папку проекта, восстановите зависимости и запустите:

```bash
cd EventsApi.Presentation
dotnet restore
dotnet run
```

По умолчанию сервис доступен по адресу `http://localhost:5102`.
Документация Swagger доступна по адресу `http://localhost:5102/swagger`.

## Валидация

- `Title`, `StartAt`, `EndAt` — обязательные поля.
- `Description` — опциональное поле (может быть `null`).
- `EndAt` должен быть строго позже `StartAt`, иначе API вернёт `400 Bad Request` с описанием ошибки в поле `EndAt` ("EndAt must be later than StartAt").
- При создании и обновлении используются отдельные DTO (`CreateEventRequest`, `UpdateEventRequest`), клиент не может передавать `Id` вручную — идентификатор генерируется сервисом автоматически.
- При создании брони (`POST /events/{id}/book`) `BookingService` проверяет существование события через `IEventRepository`; если событие не найдено, запрос завершается ошибкой `404 Not Found` до создания брони.
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

### Auth

| Метод | URL | Описание | Успех | Ошибка |
|---|---|---|---|---|
| POST | /auth/register | Регистрация пользователя (необязательное поле `role`) | 200 OK | 400 Bad Request |
| POST | /auth/login | Получение JWT-токена | 200 OK | 401 Unauthorized |

### Events

| Метод | URL | Описание | Доступ | Успех | Ошибка |
|---|---|---|---|---|---|
| GET | /events | Список событий (фильтры и пагинация) | все | 200 OK | — |
| GET | /events/{id} | Событие по id | все | 200 OK | 404 Not Found |
| POST | /events | Создать событие | Admin | 201 Created | 400, 401, 403 |
| PUT | /events/{id} | Обновить событие целиком | Admin | 204 No Content | 400, 401, 403, 404 |
| DELETE | /events/{id} | Удалить событие | Admin | 204 No Content | 401, 403, 404 |

### Bookings

| Метод | URL | Описание | Доступ | Успех | Ошибка |
|---|---|---|---|---|---|
| POST | /events/{id}/book | Создать бронь на событие | User, Admin | 202 Accepted | 400, 401, 404, 409 |
| GET | /bookings/{id} | Статус брони | владелец, Admin | 200 OK | 401, 403, 404 |
| DELETE | /bookings/{id} | Отменить бронь | владелец, Admin | 2xx | 400, 401, 403, 404 |

`POST /events/{id}/book` возвращает заголовок `Location`, указывающий на `/bookings/{bookingId}` для отслеживания статуса созданной брони.
`POST /events/{id}/book` возвращает `409 Conflict`, если событие существует, но свободных мест больше нет. Ошибка соответствует `NoAvailableSeatsException`.

## Аутентификация и роли

API использует JWT (Bearer). Токен выдаёт `POST /auth/login`, передаётся он в заголовке `Authorization: Bearer <токен>`. Токен действует один час, после этого нужно войти заново. Идентификатор и роль пользователя сервер берёт из claims токена, клиент не передаёт их в запросе.

### Роли и права

| Роль | Описание |
|---|---|
| `User` | Роль по умолчанию. Бронирует места, смотрит и отменяет свои брони. |
| `Admin` | Всё, что может `User`, плюс создание, изменение и удаление событий. Может просматривать и отменять любые брони. |

| Эндпоинт | Без токена | User | Admin |
|---|---|---|---|
| `POST /auth/register`, `POST /auth/login` | доступно | доступно | доступно |
| `GET /events`, `GET /events/{id}` | доступно | доступно | доступно |
| `POST /events`, `PUT /events/{id}`, `DELETE /events/{id}` | 401 | 403 | доступно |
| `POST /events/{id}/book` | 401 | доступно | доступно |
| `GET /bookings/{id}`, `DELETE /bookings/{id}` | 401 | только своя бронь, иначе 403 | любая бронь |

### Регистрация и роль

`POST /auth/register` принимает `login`, `password` и необязательное поле `role` (`User` или `Admin`). Без `role` создаётся `User`, неизвестное значение даёт `400`.

> Возможность указать `role = "Admin"` при регистрации оставлена только для тестирования. В продакшне её нужно убрать: иначе любой пользователь сам станет администратором.

### Правила бронирования

- Нельзя забронировать событие, которое уже началось: `400 Bad Request`.
- У одного пользователя не может быть больше 10 активных броней (`Pending` и `Confirmed`): иначе `409 Conflict`, бронь не создаётся. Лимиты разных пользователей независимы.
- Отклонённые и отменённые брони в лимит не входят.
- Отмена брони (`DELETE /bookings/{id}`) переводит её в `Cancelled` и возвращает место в `AvailableSeats`. Отменить уже отменённую или отклонённую бронь нельзя.

## Получение JWT-токена через Swagger

1. Запустите API и откройте `http://localhost:5102/swagger`.
2. Выполните `POST /auth/register`, например `{"login": "user1", "password": "Passw0rd!"}`. Для администратора добавьте `"role": "Admin"`.
3. Выполните `POST /auth/login` с теми же данными и скопируйте токен из ответа без кавычек.
4. Нажмите **Authorize**, вставьте токен в поле Value (слово `Bearer` писать не нужно), нажмите Authorize и Close.
5. Теперь запросы из Swagger уходят с заголовком `Authorization`. Чтобы сменить пользователя, нажмите Authorize → Logout и вставьте другой токен.

Проверка: `POST /events` без токена возвращает `401`, с токеном обычного пользователя `403`, с токеном администратора успех.

## Настройка секрета JWT

Параметры токена задаются в `EventsApi.Presentation/appsettings.json`:

```json
{
  "Jwt": {
    "Issuer": "EventsApi",
    "Audience": "EventsApi.Clients",
    "Secret": "<длинная-случайная-строка>",
    "ExpiresMinutes": 60
  }
}
```

Секрет подписывает токены: тот, кто его знает, может выпустить токен с любой ролью. Поэтому:

- В продакшне используйте случайную строку не короче 32 символов (для HS256 нужно минимум 256 бит) и не храните её в репозитории.
- Передавайте секрет через переменную окружения: для секции `Jwt` и ключа `Secret` это `Jwt__Secret`.
- Для локальной разработки подойдут user-secrets: `dotnet user-secrets set "Jwt:Secret" "<значение>" --project EventsApi.Presentation`.
- Значение из `appsettings.json` годится только для учебного запуска. При смене секрета все выданные токены перестают действовать.

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
| `status` | `string` | Текущий статус брони: `Pending`, `Confirmed`, `Rejected` или `Cancelled`. |
| `createdAt` | `DateTime` | Момент создания брони (UTC). |
| `processedAt` | `DateTime?` | Момент завершения обработки брони фоновым сервисом (UTC). `null`, пока бронь находится в статусе `Pending`. |

**Статусы:**

- **`Pending`** — бронь создана и ожидает обработки фоновым сервисом. Начальный статус для любой новой брони.
- **`Confirmed`** — бронь успешно обработана и подтверждена.
- **`Rejected`** — бронь отклонена во время фоновой обработки. Если для брони ранее резервировалось место, оно освобождается и возвращается в `AvailableSeats`.
- **`Cancelled`** — бронь отменена владельцем или администратором через `DELETE /bookings/{id}`; занятое место возвращается в `AvailableSeats`.

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

1. `BookingService` проверяет существование события через `IEventRepository`. Если событие не найдено, запрос завершается `404` до создания брони.
2. Если есть свободное место, `BookingService` резервирует его, создаёт `Booking` со статусом `Pending` и сохраняет изменения одним вызовом `SaveChangesAsync()`.
3. Контроллер немедленно возвращает `202 Accepted` с телом брони и заголовком `Location` — клиент не ждёт завершения обработки.
4. `BookingProcessingBackgroundService` (проект Infrastructure) каждые 5 секунд находит брони в статусе `Pending` и для каждой вызывает `IBookingProcessingService` из Application. Тот переводит бронь в `Confirmed` или `Rejected` и заполняет `processedAt`. В фоновом сервисе остались только опрос, задержки и логирование.
5. Клиент узнаёт итоговый результат, опрашивая `GET /bookings/{id}` до тех пор, пока `status` не сменится с `Pending` на конечное значение.

Такой подход разгружает HTTP-запрос от потенциально долгой обработки и позволяет вынести обработку бронирований из веб-слоя.

## Примеры запросов (curl)

Методы создания, изменения и удаления событий и все операции с бронями требуют токена. Сначала войдите и сохраните токен в переменную (ответ `/auth/login` — токен или JSON с токеном; при JSON извлеките значение поля с токеном):

```bash
TOKEN=$(curl -s -X POST http://localhost:5102/auth/login \
  -H "Content-Type: application/json" \
  -d '{"login":"admin_test","password":"Passw0rd!"}')
```

Создание событий доступно только роли `Admin`, поэтому для примеров ниже используйте токен администратора.

### Создание события

```bash
curl -X POST http://localhost:5102/events \
  -H "Authorization: Bearer $TOKEN" \
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
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"Updated Standup","startAt":"2026-07-09T11:00:00","endAt":"2026-07-09T11:30:00"}'
```

### Удаление события

```bash
curl -X DELETE http://localhost:5102/events/EVENT_ID \
  -H "Authorization: Bearer $TOKEN"
```

### Сквозной сценарий бронирования

```bash
# 1. Создать событие
curl -i -X POST http://localhost:5102/events \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"Final Check","startAt":"2026-08-10T10:00:00","endAt":"2026-08-10T11:00:00","totalSeats": 3}'
# → 201 Created, тело содержит "id" события (EVENT_ID)

# 2. Создать бронь на это событие
curl -i -X POST http://localhost:5102/events/EVENT_ID/book \
  -H "Authorization: Bearer $TOKEN"
# → 202 Accepted, Location: /bookings/BOOKING_ID, status: "Pending"

# 3. Сразу проверить статус
curl -i http://localhost:5102/bookings/BOOKING_ID \
  -H "Authorization: Bearer $TOKEN"
# → 200 OK, status: "Pending", processedAt: null

# 4. Подождать несколько секунд и проверить снова
sleep 10
curl -i http://localhost:5102/bookings/BOOKING_ID \
  -H "Authorization: Bearer $TOKEN"
# → 200 OK, status: "Confirmed", processedAt заполнен

# 5. Четвёртая бронь отклоняется

curl -i -X POST http://localhost:5102/events/EVENT_ID/book \
  -H "Authorization: Bearer $TOKEN"
# → 202 Accepted

curl -i -X POST http://localhost:5102/events/EVENT_ID/book \
  -H "Authorization: Bearer $TOKEN"
# → 202 Accepted

curl -i -X POST http://localhost:5102/events/EVENT_ID/book \
  -H "Authorization: Bearer $TOKEN"
# → 409 Conflict

# 6. Отменить бронь
curl -i -X DELETE http://localhost:5102/bookings/BOOKING_ID \
  -H "Authorization: Bearer $TOKEN"
# → успешный ответ, повторный GET покажет status: "Cancelled", место возвращено
```

Тот же сценарий можно выполнить через Swagger UI (`/swagger`): предварительно авторизовавшись (см. «Получение JWT-токена через Swagger»): последовательно вызвать `POST /events` (токен администратора), `POST /events/{id}/book`, несколько раз `GET /bookings/{id}` с задержкой, наблюдая переход статуса из `Pending` в `Confirmed`, и `DELETE /bookings/{id}`.

После трёх успешных броней `availableSeats` равен `0`. Четвёртый запрос не создаёт бронь и возвращает `409 Conflict`, что предотвращает овербукинг.

## Формат ответа при ошибках

Все ошибки API возвращаются в едином формате **Problem Details** (RFC 9110), с соответствующим HTTP-статусом:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
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
| 400 Bad Request | Ошибки валидации входных данных (пустой Title, EndAt раньше StartAt, отсутствующие обязательные поля), неизвестная роль при регистрации, бронь уже начавшегося события, отмена брони в статусе `Cancelled` или `Rejected` |
| 401 Unauthorized | Токен не передан, недействителен или просрочен; неверный логин или пароль |
| 403 Forbidden | Недостаточно прав: роль `User` вызывает методы администратора или обращается к чужой брони |
| 404 Not Found | Событие или бронь с указанным ID не найдены (GET, PUT, DELETE, POST /events/{id}/book) |
| 409 Conflict | На событии нет свободных мест (NoAvailableSeatsException) или у пользователя уже 10 активных броней (ActiveBookingsLimitExceededException) |
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

- `EventsApi.UnitTests` — unit-тесты сервисов на xUnit: `EventService` (успешные и неуспешные сценарии CRUD, фильтрации, пагинации и валидации) `BookingService` (создание брони, места, конкурентные запросы, несуществующее и удалённое событие), правила бронирования (`BookingRulesTests`: бронь прошедшего события, лимит активных броней, независимость лимитов пользователей), отмену брони (`BookingCancellationTests`), `JwtTokenService`, хеширование паролей и доменные модели `User` и `Booking`. Используют провайдер `Microsoft.EntityFrameworkCore.InMemory`, не требуют PostgreSQL и Docker. Каждый тестовый класс создаёт отдельный `ServiceProvider` и уникальную InMemory-базу через `Guid.NewGuid().ToString()`. В тестах конкурентности каждый параллельный запрос создаёт отдельный DI scope и получает собственный `AppDbContext`, но использует общую InMemory-базу тестового класса.
- `EventsApi.IntegrationTests` — интеграционные тесты репозиториев на реальной базе PostgreSQL через Testcontainers.

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
dotnet test EventsApi.UnitTests
```

Только интеграционные тесты (нужен Docker):

```bash
dotnet test EventsApi.IntegrationTests
```

Запустить тесты с отчётом о покрытии кода:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Ожидаемый результат при успешном прохождении:

```text
Сводка теста: сбой: 0, пропущено: 0
```