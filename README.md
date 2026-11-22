# Event Service Manager

Учебный проект на ASP.NET Core (.NET 10): система бронирования событий, разделённая на три микросервиса. Сервисы общаются по HTTP только с клиентом, а между собой — через Kafka. Доступ защищён JWT-аутентификацией с ролями `User` и `Admin`.

## Архитектура системы

```text
                    ┌──────────────┐
   клиент ─────────►│ UsersService │──► users-db        (регистрация, логин, выдача JWT)
      │             └──────────────┘
      │             ┌──────────────┐
      ├────────────►│EventsService │──► events-db       (события, места)
      │             └──────▲───────┘
      │                    │ подписка на топик booking-confirmed
      │                 ┌──┴──┐
      │                 │Kafka│
      │                 └──▲──┘
      │                    │ публикация BookingConfirmed
      │             ┌──────┴───────┐
      └────────────►│BookingsService│──► bookings-db    (брони)
                    └──────────────┘
```

| Сервис | Порт в Docker | Ответственность | База данных |
|---|---|---|---|
| `UsersService` | 5003 | Регистрация, вход, выдача JWT-токена. Только он выпускает токены. | `users-db` |
| `EventsService` | 5001 | CRUD событий, фильтры, пагинация. Списывает места по сообщениям из Kafka. | `events-db` |
| `BookingsService` | 5002 | Создание, просмотр и отмена броней, фоновое подтверждение, публикация `BookingConfirmed` через transactional outbox. | `bookings-db` |

У каждого сервиса своя база данных и свои миграции EF Core. Events и Bookings только проверяют токен, выданный Users: во всех трёх сервисах одинаковые `Jwt:Secret`, `Jwt:Issuer` и `Jwt:Audience`.

### Структура репозитория

```text
EventServiceManager/
├── Contracts/EventServiceManager.Contracts/   # общий контракт сообщений (BookingConfirmed, имя топика)
├── UsersService/        (Domain, Application, Infrastructure, Presentation, Tests, IntegrationTests)
├── EventsService/       (Domain, Application, Infrastructure, Presentation, Tests, IntegrationTests)
├── BookingsService/     (Domain, Application, Infrastructure, Presentation, Tests, IntegrationTests)
├── Dockerfile           # общий Dockerfile для всех сервисов (аргумент PROJECT)
└── docker-compose.yml   # инфраструктура и три сервиса
```

Каждый сервис построен по слоистой архитектуре: Domain не зависит ни от чего, Application зависит от Domain, Infrastructure от Application и Domain, Presentation — композиционный корень. Контроллеры только принимают запрос и вызывают сервис Application. Доменные исключения превращаются в HTTP-статусы в `ExceptionMapper`.

## Обмен сообщениями через Kafka

Связь между Bookings и Events построена на одном событии.

1. Bookings принимает бронь (`Pending`) и фоновый сервис её подтверждает (`Confirmed`).
2. При подтверждении Bookings в одной транзакции сохраняет статус брони и сообщение `BookingConfirmed` (содержит `BookingId`, `EventId`, `SeatsCount` и служебные поля) в таблицу `OutboxMessages`. Отдельный фоновый сервис `OutboxPublisherBackgroundService` публикует записи в топик `booking-confirmed`.
3. Events читает топик (consumer group `events-service`) и уменьшает `AvailableSeats` у события.

Параметры:

| Что | Значение |
|---|---|
| Топик | `booking-confirmed` |
| Формат | JSON (`BookingConfirmed` из `EventServiceManager.Contracts`) |
| Создание топика | `KafkaTopicInitializer` в Events при старте (3 раздела, репликация 1). Если топик существует, шаг пропускается; ошибка создания только логируется и запуск не останавливает. |
| Подписчик | `BookingConfirmedConsumer` (`BackgroundService`, слой Infrastructure сервиса Events). Для каждого сообщения создаётся отдельный DI scope. |
| Transactional outbox (Bookings) | Сообщение `BookingConfirmed` пишется в `OutboxMessages` в одной транзакции со статусом брони. `OutboxPublisherBackgroundService` выбирает неотправленные записи (`ProcessedAt IS NULL`), публикует их в Kafka и проставляет `ProcessedAt`. При ошибке (например, Kafka недоступна) растёт `Attempts`, пишется `LastError`, и запись повторяется позже: бронь остаётся `Confirmed`, сообщение не теряется. Публикация тоже «минимум один раз»: если сервис упадёт между отправкой и отметкой `ProcessedAt`, сообщение уйдёт повторно, а дубликат отсечёт идемпотентность в Events. |
| Гарантия доставки | «Минимум один раз»: оффсет подтверждается вручную после обработки. |
| Идемпотентность | Events записывает `BookingId` в таблицу `ProcessedBookings` (первичный ключ) в той же транзакции, что и списание мест. Повторное сообщение с тем же `BookingId` пропускается с `Warning` в логе, места не списываются повторно. Одновременная доставка дубликатов тоже безопасна: вторая вставка нарушает уникальность ключа и обрабатывается как дубликат. |
| Ошибки обработки | До 3 попыток с паузой 1 секунда, затем сообщение пропускается с `Error` в логе. |
| Плохие сообщения | Некорректный JSON, несуществующее событие, нехватка мест и неверное число мест пропускаются с записью в лог, подписчик не падает. |

### Ограничения такой схемы

- Bookings принимает `EventId` без проверки: он не знает, существует ли событие, не началось ли оно и есть ли места. Если события нет или мест не осталось, бронь всё равно станет `Confirmed`, а Events пропустит сообщение и запишет предупреждение в лог.
- Отмена подтверждённой брони не возвращает место в Events: отдельного события `BookingCancelled` нет.
- Очистка обработанных записей `OutboxMessages` и обработка «мёртвых» сообщений (ограничение числа попыток) не реализованы: таблица растёт, а недоставляемое сообщение повторяется бесконечно.
- Удаление события в Events не удаляет брони в Bookings: у сервисов разные базы.

## Стек технологий

- .NET 10 / ASP.NET Core Web API, Swashbuckle (Swagger UI)
- JWT Bearer-аутентификация
- Entity Framework Core, Npgsql, PostgreSQL 16, миграции EF Core
- Apache Kafka (образы Confluent 7.6.1) и клиент `Confluent.Kafka`
- Docker и Docker Compose
- xUnit, `Microsoft.EntityFrameworkCore.InMemory` (unit-тесты), Testcontainers (интеграционные тесты)

## Запуск в Docker

Требуется запущенный Docker Desktop. Из корня репозитория:

```bash
docker compose up -d --build
docker compose ps
```

Первая сборка занимает несколько минут. Поднимаются три базы PostgreSQL, Zookeeper, Kafka и три сервиса. Сервисы стартуют после готовности баз и Kafka (healthcheck), миграции применяются автоматически при старте.

| Что | Адрес |
|---|---|
| Users | `http://localhost:5003` (Swagger: `/swagger`) |
| Events | `http://localhost:5001` (Swagger: `/swagger`) |
| Bookings | `http://localhost:5002` (Swagger: `/swagger`) |
| Kafka (с хоста) | `localhost:9092` |
| PostgreSQL | events — `5432`, users — `5433`, bookings — `5434` |

Остановить систему: `docker compose down` (добавьте `-v`, чтобы удалить данные баз).

Конфигурация сервисов в контейнерах задаётся переменными окружения в `docker-compose.yml`:

| Переменная | Назначение |
|---|---|
| `ConnectionStrings__DefaultConnection` | Строка подключения к базе сервиса |
| `Jwt__Secret`, `Jwt__Issuer`, `Jwt__Audience`, `Jwt__LifetimeMinutes` | Параметры токена (одинаковые во всех сервисах) |
| `Kafka__BootstrapServers` | Адрес брокера внутри сети Docker: `kafka:29092` |
| `Kafka__ConsumerGroup` | Группа подписчика Events: `events-service` |

Для локальной отладки без контейнеров сервисов поднимите только инфраструктуру:

```bash
docker compose up -d events-db users-db bookings-db zookeeper kafka
```

Затем запускайте сервисы через `dotnet run --project <Service>/<Service>.Presentation`. Порты баз на хосте разные (5432, 5433, 5434), поэтому строку подключения Users и Bookings нужно переопределить (переменная `ConnectionStrings__DefaultConnection` или правка порта в `appsettings.json`). Адрес Kafka с хоста: `localhost:9092`.

## Схема баз и миграции

Схема каждого сервиса управляется миграциями EF Core (папка `Migrations` проекта Infrastructure). При старте сервиса вызывается `Database.MigrateAsync()`, поэтому таблицы создаются автоматически. `EnsureCreated()` не используется.

Работа с миграциями (пример для Events; для других сервисов замените имена проектов):

```bash
dotnet tool install --global dotnet-ef

dotnet ef migrations add <Name> \
  --project EventsService/EventsService.Infrastructure/EventsService.Infrastructure.csproj \
  --startup-project EventsService/EventsService.Presentation/EventsService.Presentation.csproj

dotnet ef database update \
  --project EventsService/EventsService.Infrastructure/EventsService.Infrastructure.csproj \
  --startup-project EventsService/EventsService.Presentation/EventsService.Presentation.csproj
```

Таблицы: `events` и `ProcessedBookings` (Events), `bookings` и `OutboxMessages` (Bookings), `users` (Users). `ProcessedBookings` хранит идентификаторы уже обработанных броней (`BookingId` — первичный ключ, `ProcessedAt` — время обработки). `OutboxMessages` хранит сообщения Bookings для отправки в Kafka (`Type`, `Topic`, `Key`, `Payload`, `OccurredAt`, `ProcessedAt`, `Attempts`, `LastError`); частичный индекс по `OccurredAt` ускоряет выборку записей с `ProcessedAt IS NULL`. В Bookings нет внешнего ключа на события, потому что события живут в другой базе. В `users` уникальный индекс по `Login`, пароль хранится только в виде хеша.

## Аутентификация и роли

Токен выдаёт только Users (`POST /auth/login`). Его передают в заголовке `Authorization: Bearer <токен>` в Events и Bookings. Идентификатор и роль пользователя берутся из claims токена. Срок жизни — 60 минут.

| Роль | Права |
|---|---|
| `User` | Роль по умолчанию. Создаёт брони, смотрит и отменяет свои брони. |
| `Admin` | Всё, что `User`, плюс создание, изменение и удаление событий; просмотр и отмена любых броней. |

| Эндпоинт | Без токена | User | Admin |
|---|---|---|---|
| `POST /auth/register`, `POST /auth/login` | доступно | доступно | доступно |
| `GET /events`, `GET /events/{id}` | доступно | доступно | доступно |
| `POST /events`, `PUT /events/{id}`, `DELETE /events/{id}` | 401 | 403 | доступно |
| `POST /events/{id}/book` | 401 | доступно | доступно |
| `GET /bookings/{id}`, `DELETE /bookings/{id}` | 401 | только своя бронь, иначе 403 | любая бронь |

`POST /auth/register` принимает `login`, `password` и необязательное поле `role` (`User` или `Admin`). Без `role` создаётся `User`.

> Возможность указать `role = "Admin"` при регистрации оставлена только для тестирования. В продакшене её нужно убрать: иначе любой пользователь сам станет администратором.

### Секрет JWT

Значение `Jwt:Secret` в репозитории предназначено только для разработки. В продакшене используйте случайную строку не короче 32 символов и передавайте её через переменную окружения `Jwt__Secret` (во всех трёх сервисах одно значение). При смене секрета выданные токены перестают действовать.

## Эндпоинты

### Users (`:5003`)

| Метод | URL | Описание | Успех | Ошибки |
|---|---|---|---|---|
| POST | `/auth/register` | Регистрация | 204 | 400, 409 (логин занят) |
| POST | `/auth/login` | Получение JWT | 200 | 401 |

### Events (`:5001`)

| Метод | URL | Описание | Доступ | Успех | Ошибки |
|---|---|---|---|---|---|
| GET | `/events` | Список (фильтры, пагинация) | все | 200 | — |
| GET | `/events/{id}` | Событие по id | все | 200 | 404 |
| POST | `/events` | Создать | Admin | 201 | 400, 401, 403 |
| PUT | `/events/{id}` | Обновить | Admin | 204 | 400, 401, 403, 404 |
| DELETE | `/events/{id}` | Удалить | Admin | 204 | 401, 403, 404 |

Параметры `GET /events` (все необязательные, применяются совместно): `title` (частичное совпадение без учёта регистра), `from` (`StartAt >= from`), `to` (`EndAt <= to`), `page` (по умолчанию 1), `pageSize` (по умолчанию 10). Ответ: `totalCount`, `items`, `page`, `pageSize`.

### Bookings (`:5002`)

| Метод | URL | Описание | Доступ | Успех | Ошибки |
|---|---|---|---|---|---|
| POST | `/events/{id}/book` | Создать бронь | User, Admin | 202 | 400, 401, 409 |
| GET | `/bookings/{id}` | Статус брони | владелец, Admin | 200 | 401, 403, 404 |
| DELETE | `/bookings/{id}` | Отменить бронь | владелец, Admin | 204 | 400, 401, 403, 404 |

`POST /events/{id}/book` возвращает `202 Accepted` и заголовок `Location: /bookings/{bookingId}`.

## Модели

### Event

| Поле | Тип | Описание |
|---|---|---|
| `id` | `Guid` | Идентификатор. |
| `title` | `string` | Название (обязательное). |
| `description` | `string?` | Описание. |
| `startAt`, `endAt` | `DateTime` | Начало и окончание; `endAt` строго позже `startAt`. |
| `totalSeats` | `int` | Вместимость, больше 0. |
| `availableSeats` | `int` | Свободные места. При создании равно `totalSeats`, уменьшается после получения `BookingConfirmed`. Клиент его не передаёт. |

### Booking

| Поле | Тип | Описание |
|---|---|---|
| `id` | `Guid` | Идентификатор брони. |
| `eventId` | `Guid` | Событие, на которое сделана бронь. |
| `status` | `string` | `Pending`, `Confirmed`, `Rejected` или `Cancelled`. |
| `createdAt` | `DateTime` | Время создания (UTC). |
| `processedAt` | `DateTime?` | Время обработки фоновым сервисом; `null`, пока бронь `Pending`. |

Статусы: `Pending` — создана и ждёт обработки; `Confirmed` — подтверждена, сообщение записано в outbox и будет отправлено в Kafka; `Rejected` — отклонена при обработке; `Cancelled` — отменена владельцем или администратором.

## Правила бронирования

- У одного пользователя не больше 10 активных броней (`Pending` и `Confirmed`): иначе `409 Conflict`. Лимиты пользователей независимы, `Rejected` и `Cancelled` в лимит не входят.
- `EventId` должен быть непустым, иначе `400`.
- Отменить можно бронь в статусе `Pending` или `Confirmed`; отмену `Cancelled` и `Rejected` сервис отклоняет с `400`. Чужую бронь отменить нельзя (`403`).
- Bookings не проверяет событие: ошибки `404` для несуществующего события, `400` для начавшегося и `409` для отсутствия мест здесь больше не возвращаются (см. «Ограничения такой схемы»).

### Фоновая обработка

`POST /events/{id}/book` не обрабатывает бронь синхронно: создаётся `Booking` в статусе `Pending` и сразу возвращается `202`. `BookingProcessingBackgroundService` каждые 5 секунд находит брони `Pending`, обрабатывает каждую в отдельном scope, переводит в `Confirmed` или `Rejected` и для подтверждённых сохраняет `BookingConfirmed` в `OutboxMessages` в одной транзакции со статусом. В Kafka сообщение отправляет `OutboxPublisherBackgroundService`. Клиент опрашивает `GET /bookings/{id}`.

## Сквозной сценарий (PowerShell)

Скрипт проверяет всю цепочку: Users → Events → Bookings → Kafka → Events. Работает в Windows PowerShell 5.1 и PowerShell 7, система должна быть запущена (`docker compose up -d --build`).

```powershell
$u = "http://localhost:5003"; $e = "http://localhost:5001"; $b = "http://localhost:5002"
$json = "application/json"; $rnd = Get-Random; $pass = "Passw0rd!"
$userLogin = "user$rnd"; $adminLogin = "admin$rnd"

function Reg($login, $role) {
  $body = @{ login = $login; password = $pass }
  if ($role) { $body.role = $role }
  Invoke-RestMethod "$u/auth/register" -Method Post -ContentType $json -Body ($body | ConvertTo-Json) | Out-Null
}
function Login($login) {
  (Invoke-RestMethod "$u/auth/login" -Method Post -ContentType $json -Body (@{ login = $login; password = $pass } | ConvertTo-Json)).token
}

# 1. Users: регистрация и токены
Reg $userLogin $null
Reg $adminLogin "Admin"
$userToken = Login $userLogin
$adminToken = Login $adminLogin
if (-not $userToken -or -not $adminToken) { throw "Токен не получен" }
"1. Токены получены"

# 2. Events: администратор создаёт событие на 5 мест
$start = (Get-Date).ToUniversalTime().AddDays(30)
$ev = Invoke-RestMethod "$e/events" -Method Post -ContentType $json -Headers @{ Authorization = "Bearer $adminToken" } `
  -Body (@{ title = "Kafka e2e $rnd"; startAt = $start.ToString("o"); endAt = $start.AddHours(2).ToString("o"); totalSeats = 5 } | ConvertTo-Json)
$before = (Invoke-RestMethod "$e/events/$($ev.id)").availableSeats
"2. Событие $($ev.id), мест до брони: $before"

# 3. Bookings: бронь (Pending) и ожидание Confirmed
$bk = Invoke-RestMethod "$b/events/$($ev.id)/book" -Method Post -Headers @{ Authorization = "Bearer $userToken" }
$status = $bk.status
for ($i = 0; $i -lt 20 -and $status -ne "Confirmed"; $i++) {
  Start-Sleep 1
  $status = (Invoke-RestMethod "$b/bookings/$($bk.id)" -Headers @{ Authorization = "Bearer $userToken" }).status
}
"3. Статус брони: $status"

# 4. Events: места уменьшились (сообщение прошло через Kafka)
$after = $before
for ($i = 0; $i -lt 15 -and $after -ge $before; $i++) {
  Start-Sleep 1
  $after = (Invoke-RestMethod "$e/events/$($ev.id)").availableSeats
}
"4. Мест после брони: $after (ожидалось $($before - 1))"
```

Ожидаемый вывод: статус `Confirmed` и число мест на единицу меньше исходного.

### Как убедиться, что места списала именно Kafka

Events и Bookings не вызывают друг друга по HTTP: в коде сервисов нет `HttpClient`, а в конфигурации нет адресов соседних сервисов. Единственный путь изменения мест — подписчик `BookingConfirmedConsumer`. Это видно в логах:

```powershell
docker compose logs events-service | Select-String "Reserved"
```

Ожидаемая строка: `Reserved 1 seat(s) of event <eventId> for booking <bookingId>.`

Проверка устойчивости: остановите Events (`docker compose stop events-service`), создайте и подтвердите ещё одну бронь, затем запустите сервис (`docker compose start events-service`). Подписчик прочитает сохранённое сообщение, и места уменьшатся уже после старта.

Так же проверяется outbox в Bookings: остановите Kafka (`docker compose stop kafka`) и создайте бронь. Она станет `Confirmed`, а в `OutboxMessages` останется запись с `ProcessedAt = null` (растёт `Attempts`). После `docker compose start kafka` запись получит `ProcessedAt`, а места в Events уменьшатся ровно один раз.

## Формат ошибок

Все сервисы возвращают ошибки в формате Problem Details (RFC 9110) с полями `type`, `title`, `status`, `detail`, `instance` и `traceId`.

| Статус | Когда |
|---|---|
| 400 | Ошибки валидации, неизвестная роль при регистрации, пустой `EventId`, отмена брони в конечном статусе |
| 401 | Нет токена, он недействителен или просрочен; неверный логин или пароль |
| 403 | Недостаточно прав; обращение к чужой брони |
| 404 | Событие или бронь не найдены |
| 409 | Лимит 10 активных броней, логин занят, конфликт параллельного изменения |
| 500 | Непредвиденные ошибки |

Клиентские ошибки (4xx) пишутся в лог на уровне `Warning`, серверные (5xx) — на уровне `Error`.

## Тесты

В каждом сервисе два проекта: `*.Tests` (unit-тесты на InMemory, без Docker) и `*.IntegrationTests` (PostgreSQL в Testcontainers, нужен запущенный Docker). Интеграционные тесты поднимают приложение через `WebApplicationFactory<Program>`; в тестах Events hosted-сервисы Kafka отключены, подписчик проверяется отдельно unit-тестами `BookingConfirmedHandler`.

```bash
dotnet test                                              # всё (нужен Docker)
dotnet test EventsService/EventsService.Tests            # unit-тесты Events
dotnet test EventsService/EventsService.IntegrationTests
dotnet test BookingsService/BookingsService.Tests
dotnet test BookingsService/BookingsService.IntegrationTests
dotnet test UsersService/UsersService.Tests
dotnet test UsersService/UsersService.IntegrationTests
```

Покрытие: правила бронирования и отмены, подтверждение брони, запись `BookingConfirmed` в outbox в одной транзакции с подтверждением брони, обработчик `BookingConfirmed` в Events (списание мест, событие не найдено, нехватка мест, неверное число мест, повторная и параллельная доставка одного сообщения), репозитории на реальной PostgreSQL, HTTP-тесты авторизации (401, 403, чужая бронь, повторная регистрация, неверный пароль).

Если Docker не запущен, интеграционные тесты завершатся ошибкой `DockerUnavailableException`: запустите Docker Desktop и проверьте `docker ps`.
