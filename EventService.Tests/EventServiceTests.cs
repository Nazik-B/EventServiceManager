using System.ComponentModel.DataAnnotations;
using EventsApi.Models.Dto;
using EventsApi.Services;
using Xunit;

namespace EventService.Tests;

public class EventServiceTests
{
    private static EventsApi.Services.EventService CreateService(
        out EventsApi.DataAccess.AppDbContext context)
    {
        context = TestDbContextFactory.Create();

        return new EventsApi.Services.EventService(context);
    }

    private static CreateEventRequest BuildRequest(
        string title,
        DateTime start,
        DateTime end,
        string? description = null)
    {
        return new CreateEventRequest
        {
            Title = title,
            Description = description,
            StartAt = start,
            EndAt = end,
            TotalSeats = 10
        };
    }

    [Fact]
    public async Task CreateAsync_AddsEvent_AndReturnsItWithGeneratedId()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var request = BuildRequest(
            "Team Meeting",
            DateTime.Parse("2026-08-01T10:00:00"),
            DateTime.Parse("2026-08-01T11:00:00"));

        var created = await service.CreateAsync(request);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Team Meeting", created.Title);
        Assert.Equal(request.StartAt, created.StartAt);
        Assert.Equal(request.EndAt, created.EndAt);
    }

    [Fact]
    public async Task CreateAsync_AssignsUniqueIds_ForMultipleEvents()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var first = await service.CreateAsync(BuildRequest(
            "Event 1",
            DateTime.Parse("2026-08-01T10:00:00"),
            DateTime.Parse("2026-08-01T11:00:00")));

        var second = await service.CreateAsync(BuildRequest(
            "Event 2",
            DateTime.Parse("2026-08-02T10:00:00"),
            DateTime.Parse("2026-08-02T11:00:00")));

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task CreateAsync_ThrowsValidationException_WhenEndAtBeforeStartAt()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var request = BuildRequest(
            "Invalid Event",
            DateTime.Parse("2026-08-01T12:00:00"),
            DateTime.Parse("2026-08-01T10:00:00"));

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(request));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyResult_WhenNoEventsExist()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var result = await service.GetAllAsync(
            null,
            null,
            null,
            page: 1,
            pageSize: 10);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEvents_WhenNoFiltersApplied()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        await service.CreateAsync(BuildRequest(
            "Event 1",
            DateTime.Parse("2026-08-01T10:00:00"),
            DateTime.Parse("2026-08-01T11:00:00")));

        await service.CreateAsync(BuildRequest(
            "Event 2",
            DateTime.Parse("2026-08-02T10:00:00"),
            DateTime.Parse("2026-08-02T11:00:00")));

        var result = await service.GetAllAsync(
            null,
            null,
            null,
            page: 1,
            pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count());
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEvent_WhenExists()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var created = await service.CreateAsync(BuildRequest(
            "Team Meeting",
            DateTime.Parse("2026-08-01T10:00:00"),
            DateTime.Parse("2026-08-01T11:00:00")));

        var found = await service.GetByIdAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal("Team Meeting", found.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var found = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public async Task UpdateAsync_ModifiesEvent_WhenExists()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var created = await service.CreateAsync(BuildRequest(
            "Old Title",
            DateTime.Parse("2026-08-01T10:00:00"),
            DateTime.Parse("2026-08-01T11:00:00")));

        var updateRequest = new UpdateEventRequest
        {
            Title = "New Title",
            Description = "Updated description",
            StartAt = DateTime.Parse("2026-08-05T09:00:00"),
            EndAt = DateTime.Parse("2026-08-05T10:00:00")
        };

        var result = await service.UpdateAsync(created.Id, updateRequest);
        var updated = await service.GetByIdAsync(created.Id);

        Assert.True(result);
        Assert.NotNull(updated);
        Assert.Equal("New Title", updated!.Title);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(updateRequest.StartAt, updated.StartAt);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFalse_WhenEventNotFound()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var updateRequest = new UpdateEventRequest
        {
            Title = "New Title",
            StartAt = DateTime.Parse("2026-08-05T09:00:00"),
            EndAt = DateTime.Parse("2026-08-05T10:00:00")
        };

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            updateRequest);

        Assert.False(result);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsValidationException_WhenEndAtBeforeStartAt()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var created = await service.CreateAsync(BuildRequest(
            "Original",
            DateTime.Parse("2026-08-01T10:00:00"),
            DateTime.Parse("2026-08-01T11:00:00")));

        var updateRequest = new UpdateEventRequest
        {
            Title = "Broken Update",
            StartAt = DateTime.Parse("2026-08-05T12:00:00"),
            EndAt = DateTime.Parse("2026-08-05T10:00:00")
        };

        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateAsync(created.Id, updateRequest));
    }

    [Fact]
    public async Task DeleteAsync_RemovesEvent_WhenExists()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var created = await service.CreateAsync(BuildRequest(
            "To Delete",
            DateTime.Parse("2026-08-01T10:00:00"),
            DateTime.Parse("2026-08-01T11:00:00")));

        var result = await service.DeleteAsync(created.Id);
        var found = await service.GetByIdAsync(created.Id);

        Assert.True(result);
        Assert.Null(found);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenEventNotFound()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(result);
    }

    [Theory]
    [InlineData("meeting", 1)]
    [InlineData("MEETING", 1)]
    [InlineData("review", 1)]
    [InlineData("nonexistent", 0)]
    public async Task GetAllAsync_FiltersByTitle(
        string titleFilter,
        int expectedCount)
    {
        var service = CreateService(out var context);
        await using var _ = context;

        await service.CreateAsync(BuildRequest(
            "Team Meeting",
            DateTime.Parse("2026-08-01T10:00:00"),
            DateTime.Parse("2026-08-01T11:00:00")));

        await service.CreateAsync(BuildRequest(
            "Code Review",
            DateTime.Parse("2026-08-02T10:00:00"),
            DateTime.Parse("2026-08-02T11:00:00")));

        var result = await service.GetAllAsync(
            titleFilter,
            null,
            null,
            page: 1,
            pageSize: 10);

        Assert.Equal(expectedCount, result.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByFromDate_ExcludesEarlierEvents()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        await service.CreateAsync(BuildRequest(
            "Early Event",
            DateTime.Parse("2026-07-01T10:00:00"),
            DateTime.Parse("2026-07-01T11:00:00")));

        await service.CreateAsync(BuildRequest(
            "Late Event",
            DateTime.Parse("2026-08-15T10:00:00"),
            DateTime.Parse("2026-08-15T11:00:00")));

        var result = await service.GetAllAsync(
            null,
            DateTime.Parse("2026-08-01T00:00:00"),
            null,
            page: 1,
            pageSize: 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Late Event", result.Items.Single().Title);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByToDate_ExcludesLaterEvents()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        await service.CreateAsync(BuildRequest(
            "Early Event",
            DateTime.Parse("2026-07-01T10:00:00"),
            DateTime.Parse("2026-07-01T11:00:00")));

        await service.CreateAsync(BuildRequest(
            "Late Event",
            DateTime.Parse("2026-08-15T10:00:00"),
            DateTime.Parse("2026-08-15T11:00:00")));

        var result = await service.GetAllAsync(
            null,
            null,
            DateTime.Parse("2026-07-31T23:59:59"),
            page: 1,
            pageSize: 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Early Event", result.Items.Single().Title);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCorrectPageSize()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        for (var i = 1; i <= 3; i++)
        {
            await service.CreateAsync(BuildRequest(
                $"Event {i}",
                DateTime.Parse("2026-08-01T10:00:00"),
                DateTime.Parse("2026-08-01T11:00:00")));
        }

        var result = await service.GetAllAsync(
            null,
            null,
            null,
            page: 2,
            pageSize: 2);

        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task GetAllAsync_CombinesTitleAndDateFilters()
    {
        var service = CreateService(out var context);
        await using var _ = context;

        await service.CreateAsync(BuildRequest(
            "Team Standup",
            DateTime.Parse("2026-08-01T09:00:00"),
            DateTime.Parse("2026-08-01T09:30:00")));

        await service.CreateAsync(BuildRequest(
            "Team Retro",
            DateTime.Parse("2026-08-15T14:00:00"),
            DateTime.Parse("2026-08-15T15:00:00")));

        await service.CreateAsync(BuildRequest(
            "Client Call",
            DateTime.Parse("2026-08-10T11:00:00"),
            DateTime.Parse("2026-08-10T12:00:00")));

        await service.CreateAsync(BuildRequest(
            "Team Planning",
            DateTime.Parse("2026-09-01T10:00:00"),
            DateTime.Parse("2026-09-01T11:00:00")));

        var result = await service.GetAllAsync(
            "team",
            DateTime.Parse("2026-08-01T00:00:00"),
            DateTime.Parse("2026-08-31T23:59:59"),
            page: 1,
            pageSize: 10);

        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, e => e.Title == "Team Standup");
        Assert.Contains(result.Items, e => e.Title == "Team Retro");
        Assert.DoesNotContain(result.Items, e => e.Title == "Client Call");
        Assert.DoesNotContain(result.Items, e => e.Title == "Team Planning");
    }

    [Fact]
    public void CreateEventRequest_FailsValidation_WhenTitleIsEmpty()
    {
        var request = new CreateEventRequest
        {
            Title = "",
            StartAt = DateTime.Parse("2026-08-01T10:00:00"),
            EndAt = DateTime.Parse("2026-08-01T11:00:00"),
            TotalSeats = 10
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
            EndAt = DateTime.Parse("2026-08-01T12:00:00"),
            TotalSeats = 10
        };

        var validationResults = ValidateModel(request);

        Assert.Empty(validationResults);
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            model,
            context,
            results,
            validateAllProperties: true);

        return results;
    }
}