using System.ComponentModel.DataAnnotations;
using EventsApi.Models.Dto;
using EventsApi.Services;
using Xunit;

namespace EventService.Tests;

public class EventServiceTests
{
    private static EventsApi.Services.EventService CreateService() => new();

    private static CreateEventRequest BuildRequest(string title, DateTime start, DateTime end, string? description = null) =>
        new()
        {
            Title = title,
            Description = description,
            StartAt = start,
            EndAt = end
        };

    // 1. Создание события
    [Fact]
    public void Create_AddsEvent_AndReturnsItWithGeneratedId()
    {
        var service = CreateService();
        var request = BuildRequest("Team Meeting", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00"));

        var created = service.Create(request);

        Assert.Equal(1, created.Id);
        Assert.Equal("Team Meeting", created.Title);
        Assert.Equal(request.StartAt, created.StartAt);
        Assert.Equal(request.EndAt, created.EndAt);
    }

    [Fact]
    public void Create_AssignsIncrementalIds_ForMultipleEvents()
    {
        var service = CreateService();

        var first = service.Create(BuildRequest("Event 1", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));
        var second = service.Create(BuildRequest("Event 2", DateTime.Parse("2026-08-02T10:00:00"), DateTime.Parse("2026-08-02T11:00:00")));

        Assert.Equal(1, first.Id);
        Assert.Equal(2, second.Id);
    }

    // 2. Получение всех событий
    [Fact]
    public void GetAll_ReturnsEmptyResult_WhenNoEventsExist()
    {
        var service = CreateService();

        var result = service.GetAll(null, null, null, page: 1, pageSize: 10);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void GetAll_ReturnsAllEvents_WhenNoFiltersApplied()
    {
        var service = CreateService();
        service.Create(BuildRequest("Event 1", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));
        service.Create(BuildRequest("Event 2", DateTime.Parse("2026-08-02T10:00:00"), DateTime.Parse("2026-08-02T11:00:00")));

        var result = service.GetAll(null, null, null, page: 1, pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count());
    }

    // 3. Получение события по ID
    [Fact]
    public void GetById_ReturnsEvent_WhenExists()
    {
        var service = CreateService();
        var created = service.Create(BuildRequest("Team Meeting", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));

        var found = service.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal("Team Meeting", found.Title);
    }

    [Fact]
    public void GetById_ReturnsNull_WhenNotExists()
    {
        var service = CreateService();

        var found = service.GetById(999);

        Assert.Null(found);
    }

    // 4. Обновление существующего события
    [Fact]
    public void Update_ModifiesEvent_WhenExists()
    {
        var service = CreateService();
        var created = service.Create(BuildRequest("Old Title", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));

        var updateRequest = new UpdateEventRequest
        {
            Title = "New Title",
            Description = "Updated description",
            StartAt = DateTime.Parse("2026-08-05T09:00:00"),
            EndAt = DateTime.Parse("2026-08-05T10:00:00")
        };

        var result = service.Update(created.Id, updateRequest);
        var updated = service.GetById(created.Id);

        Assert.True(result);
        Assert.Equal("New Title", updated!.Title);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(updateRequest.StartAt, updated.StartAt);
    }

    [Fact]
    public void Update_ReturnsFalse_WhenEventNotFound()
    {
        var service = CreateService();
        var updateRequest = new UpdateEventRequest
        {
            Title = "New Title",
            StartAt = DateTime.Parse("2026-08-05T09:00:00"),
            EndAt = DateTime.Parse("2026-08-05T10:00:00")
        };

        var result = service.Update(999, updateRequest);

        Assert.False(result);
    }

    // 5. Удаление существующего события
    [Fact]
    public void Delete_RemovesEvent_WhenExists()
    {
        var service = CreateService();
        var created = service.Create(BuildRequest("To Delete", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));

        var result = service.Delete(created.Id);
        var found = service.GetById(created.Id);

        Assert.True(result);
        Assert.Null(found);
    }

    [Fact]
    public void Delete_ReturnsFalse_WhenEventNotFound()
    {
        var service = CreateService();

        var result = service.Delete(999);

        Assert.False(result);
    }

    // 6. Фильтрация по названию
    [Theory]
    [InlineData("meeting", 1)]
    [InlineData("MEETING", 1)]
    [InlineData("review", 1)]
    [InlineData("nonexistent", 0)]
    public void GetAll_FiltersByTitle_CaseInsensitivePartialMatch(string titleFilter, int expectedCount)
    {
        var service = CreateService();
        service.Create(BuildRequest("Team Meeting", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));
        service.Create(BuildRequest("Code Review", DateTime.Parse("2026-08-02T10:00:00"), DateTime.Parse("2026-08-02T11:00:00")));

        var result = service.GetAll(titleFilter, null, null, page: 1, pageSize: 10);

        Assert.Equal(expectedCount, result.TotalCount);
    }

    // 7. Фильтрация по датам (from/to)
    [Fact]
    public void GetAll_FiltersByFromDate_ExcludesEarlierEvents()
    {
        var service = CreateService();
        service.Create(BuildRequest("Early Event", DateTime.Parse("2026-07-01T10:00:00"), DateTime.Parse("2026-07-01T11:00:00")));
        service.Create(BuildRequest("Late Event", DateTime.Parse("2026-08-15T10:00:00"), DateTime.Parse("2026-08-15T11:00:00")));

        var result = service.GetAll(null, from: DateTime.Parse("2026-08-01T00:00:00"), to: null, page: 1, pageSize: 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Late Event", result.Items.First().Title);
    }

    [Fact]
    public void GetAll_FiltersByToDate_ExcludesLaterEvents()
    {
        var service = CreateService();
        service.Create(BuildRequest("Early Event", DateTime.Parse("2026-07-01T10:00:00"), DateTime.Parse("2026-07-01T11:00:00")));
        service.Create(BuildRequest("Late Event", DateTime.Parse("2026-08-15T10:00:00"), DateTime.Parse("2026-08-15T11:00:00")));

        var result = service.GetAll(null, from: null, to: DateTime.Parse("2026-07-31T23:59:59"), page: 1, pageSize: 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Early Event", result.Items.First().Title);
    }

    [Fact]
    public void GetAll_FiltersByDateRange_ReturnsOnlyEventsWithinRange()
    {
        var service = CreateService();
        service.Create(BuildRequest("Before Range", DateTime.Parse("2026-07-01T10:00:00"), DateTime.Parse("2026-07-01T11:00:00")));
        service.Create(BuildRequest("Inside Range", DateTime.Parse("2026-08-10T10:00:00"), DateTime.Parse("2026-08-10T11:00:00")));
        service.Create(BuildRequest("After Range", DateTime.Parse("2026-09-01T10:00:00"), DateTime.Parse("2026-09-01T11:00:00")));

        var result = service.GetAll(null,
            from: DateTime.Parse("2026-08-01T00:00:00"),
            to: DateTime.Parse("2026-08-31T23:59:59"),
            page: 1, pageSize: 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Inside Range", result.Items.First().Title);
    }

    // 8. Пагинация событий
    [Theory]
    [InlineData(1, 2, 2)]
    [InlineData(2, 2, 1)]
    [InlineData(1, 10, 3)]
    public void GetAll_ReturnsCorrectPageSize(int page, int pageSize, int expectedCount)
    {
        var service = CreateService();
        for (int i = 1; i <= 3; i++)
        {
            service.Create(BuildRequest($"Event {i}", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));
        }

        var result = service.GetAll(null, null, null, page, pageSize);

        Assert.Equal(expectedCount, result.Items.Count());
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public void GetAll_PaginationPreservesTotalCount_AcrossPages()
    {
        var service = CreateService();
        for (int i = 1; i <= 5; i++)
        {
            service.Create(BuildRequest($"Event {i}", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));
        }

        var page1 = service.GetAll(null, null, null, page: 1, pageSize: 2);
        var page2 = service.GetAll(null, null, null, page: 2, pageSize: 2);
        var page3 = service.GetAll(null, null, null, page: 3, pageSize: 2);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(5, page3.TotalCount);
        Assert.Equal(2, page1.Items.Count());
        Assert.Equal(2, page2.Items.Count());
        Assert.Single(page3.Items);
    }

    // 9. Комбинированная фильтрация (title + from + to + pagination)
    [Fact]
    public void GetAll_CombinesTitleAndDateFilters_WithPagination()
    {
        var service = CreateService();
        service.Create(BuildRequest("Team Standup", DateTime.Parse("2026-08-01T09:00:00"), DateTime.Parse("2026-08-01T09:30:00")));
        service.Create(BuildRequest("Team Retro", DateTime.Parse("2026-08-15T14:00:00"), DateTime.Parse("2026-08-15T15:00:00")));
        service.Create(BuildRequest("Client Call", DateTime.Parse("2026-08-10T11:00:00"), DateTime.Parse("2026-08-10T12:00:00")));
        service.Create(BuildRequest("Team Planning", DateTime.Parse("2026-09-01T10:00:00"), DateTime.Parse("2026-09-01T11:00:00")));

        var result = service.GetAll(
            title: "team",
            from: DateTime.Parse("2026-08-01T00:00:00"),
            to: DateTime.Parse("2026-08-31T23:59:59"),
            page: 1, pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, e => e.Title == "Team Standup");
        Assert.Contains(result.Items, e => e.Title == "Team Retro");
        Assert.DoesNotContain(result.Items, e => e.Title == "Client Call");
        Assert.DoesNotContain(result.Items, e => e.Title == "Team Planning");
    }

    // 10. Получение события с несуществующим ID (дополнительный кейс)
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999999)]
    public void GetById_ReturnsNull_ForVariousNonExistentIds(int id)
    {
        var service = CreateService();
        service.Create(BuildRequest("Existing Event", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));

        var found = service.GetById(id);

        Assert.Null(found);
    }

    // 11. Обновление события с несуществующим ID (дополнительный кейс)
    [Fact]
    public void Update_DoesNotAffectExistingEvents_WhenIdNotFound()
    {
        var service = CreateService();
        var created = service.Create(BuildRequest("Original", DateTime.Parse("2026-08-01T10:00:00"), DateTime.Parse("2026-08-01T11:00:00")));

        var updateRequest = new UpdateEventRequest
        {
            Title = "Should Not Apply",
            StartAt = DateTime.Parse("2026-09-01T10:00:00"),
            EndAt = DateTime.Parse("2026-09-01T11:00:00")
        };

        var result = service.Update(999, updateRequest);
        var unchanged = service.GetById(created.Id);

        Assert.False(result);
        Assert.Equal("Original", unchanged!.Title);
    }

    // 12. Создание события с некорректными данными (валидация DataAnnotations на уровне DTO)
    [Fact]
    public void CreateEventRequest_FailsValidation_WhenTitleIsEmpty()
    {
        var request = new CreateEventRequest
        {
            Title = "",
            StartAt = DateTime.Parse("2026-08-01T10:00:00"),
            EndAt = DateTime.Parse("2026-08-01T11:00:00")
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(CreateEventRequest.Title)));
    }

    [Fact]
    public void CreateEventRequest_FailsValidation_WhenStartAtIsNull()
    {
        var request = new CreateEventRequest
        {
            Title = "Valid Title",
            StartAt = null,
            EndAt = DateTime.Parse("2026-08-01T11:00:00")
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    // 13. Обновление события с некорректными датами (EndAt раньше StartAt)
    [Fact]
    public void UpdateEventRequest_FailsValidation_WhenEndAtIsBeforeStartAt()
    {
        var request = new UpdateEventRequest
        {
            Title = "Invalid Dates",
            StartAt = DateTime.Parse("2026-08-01T12:00:00"),
            EndAt = DateTime.Parse("2026-08-01T10:00:00")
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("EndAt") || r.ErrorMessage.Contains("StartAt"));
    }

    [Fact]
    public void CreateEventRequest_FailsValidation_WhenEndAtIsBeforeStartAt()
    {
        var request = new CreateEventRequest
        {
            Title = "Invalid Dates",
            StartAt = DateTime.Parse("2026-08-01T12:00:00"),
            EndAt = DateTime.Parse("2026-08-01T10:00:00")
        };

        var validationResults = ValidateModel(request);

        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void CreateEventRequest_PassesValidation_WhenDatesAreValid()
    {
        var request = new CreateEventRequest
        {
            Title = "Valid Event",
            StartAt = DateTime.Parse("2026-08-01T10:00:00"),
            EndAt = DateTime.Parse("2026-08-01T12:00:00")
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}