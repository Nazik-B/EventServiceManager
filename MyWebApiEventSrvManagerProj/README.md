# Event Service Manager API

Учебный проект ASP.NET Core Web API для управления событиями (Event). Реализован CRUD, валидация входных данных и документация через Swagger.

## Стек технологий

- .NET 10 / ASP.NET Core Web API
- Swashbuckle.AspNetCore (Swagger UI)
- In-memory хранилище (List<Event> в сервисе)

## Требования

- Установленный .NET SDK 10.0 или новее
- Git

## Запуск проекта

Клонируйте репозиторий и перейдите в ветку `sprint-1`, где ведётся вся разработка:

```bash
git clone <URL_репозитория>
cd EventServiceManager
git checkout sprint-1
```

Перейдите в папку с файлом проекта (`.csproj`) и восстановите зависимости:

```bash
cd MyWebApiEventSrvManagerProj
dotnet restore
```

Запустите приложение:

```bash
dotnet run
```

## Валидация

- `Title`, `StartAt`, `EndAt` — обязательные поля.
- `EndAt` должен быть строго позже `StartAt`, иначе API вернёт `400 Bad Request` с описанием ошибки.
- При создании и обновлении используются отдельные DTO (`CreateEventRequest`, `UpdateEventRequest`), клиент не может передавать `Id` вручную.

## Эндпоинты API

| Метод  | URL              | Описание                          | Успех         | Ошибка              |
|--------|------------------|------------------------------------|---------------|----------------------|
| GET    | /events          | Получить список всех событий       | 200 OK        | —                    |
| GET    | /events/{id}     | Получить событие по id             | 200 OK        | 404 Not Found        |
| POST   | /events          | Создать новое событие              | 201 Created   | 400 Bad Request      |
| PUT    | /events/{id}     | Обновить событие целиком           | 204 No Content| 404 Not Found, 400 Bad Request |
| DELETE | /events/{id}     | Удалить событие                    | 204 No Content| 404 Not Found        |

## Примеры запросов (curl)

### Создание события

```bash
curl -X POST http://localhost:5102/events \
  -H "Content-Type: application/json" \
  -d '{"title":"Standup","description":"Daily meeting","startAt":"2026-07-09T10:00:00","endAt":"2026-07-09T10:15:00"}'
```

### Получение списка событий

```bash
curl http://localhost:5102/events
```

### Получение события по id

```bash
curl http://localhost:5102/events/1
```

### Обновление события

```bash
curl -X PUT http://localhost:5102/events/1 \
  -H "Content-Type: application/json" \
  -d '{"title":"Updated Standup","startAt":"2026-07-09T11:00:00","endAt":"2026-07-09T11:30:00"}'
```

### Удаление события

```bash
curl -X DELETE http://localhost:5102/events/1
```