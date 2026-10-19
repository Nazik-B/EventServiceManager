using EventsApi.Models;
using EventsApi.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventApi.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class EventRepositoryTests : IntegrationTestBase
{
    public EventRepositoryTests(PostgresTestContainer fixture) : base(fixture) { }

    private EventRepository CreateRepository() => new(DbContext);

    private static Event CreateEvent(string title, DateTime start, DateTime end, int seats = 10) =>
        Event.Create(Guid.NewGuid(), title, "desc", start, end, seats);

    [Fact]
    public async Task AddAsync_SavesEventToDatabase()
    {
        var repo = CreateRepository();
        var ev = CreateEvent("Концерт", DateTime.UtcNow, DateTime.UtcNow.AddHours(2));

        await repo.AddAsync(ev);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var fromDb = await DbContext.Events.FindAsync(ev.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("Концерт", fromDb.Title);
        Assert.Equal(ev.TotalSeats, fromDb.AvailableSeats);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEvent_WhenExists()
    {
        var repo = CreateRepository();
        var ev = CreateEvent("Концерт", DateTime.UtcNow, DateTime.UtcNow.AddHours(2));
        await repo.AddAsync(ev);
        await repo.SaveChangesAsync();        
        DbContext.ChangeTracker.Clear();

        var result = await repo.GetByIdAsync(ev.Id);

        Assert.NotNull(result);
        Assert.Equal(ev.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_ReturnsTrackedEntity()
    {
        var repo = CreateRepository();
        var ev = CreateEvent("Концерт", DateTime.UtcNow, DateTime.UtcNow.AddHours(2));
        await repo.AddAsync(ev);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await repo.GetByIdForUpdateAsync(ev.Id);

        Assert.NotNull(result);
        Assert.Equal(EntityState.Unchanged, DbContext.Entry(result!).State);
    }

    [Fact]
    public async Task Delete_RemovesEventFromDatabase()
    {
        var repo = CreateRepository();
        var ev = CreateEvent("Концерт", DateTime.UtcNow, DateTime.UtcNow.AddHours(2));
        await repo.AddAsync(ev);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetByIdForUpdateAsync(ev.Id);
        repo.Delete(tracked!);
        await repo.SaveChangesAsync();

        Assert.Null(await DbContext.Events.FindAsync(ev.Id));
    }

    [Fact]
    public async Task GetPagedAsync_NoFilters_PaginatesCorrectly()
    {
        var repo = CreateRepository();
        for (int i = 0; i < 5; i++)
        {
            var ev = CreateEvent($"Событие {i}",
                DateTime.UtcNow.AddHours(i), DateTime.UtcNow.AddHours(i + 1));
            await repo.AddAsync(ev);
        }
        await repo.SaveChangesAsync();

        var (page1, total1) = await repo.GetPagedAsync(null, null, null, 1, 2);
        var (page2, total2) = await repo.GetPagedAsync(null, null, null, 2, 2);

        Assert.Equal(5, total1);
        Assert.Equal(2, page1.Count);
        Assert.Equal(5, total2);
        Assert.Equal(2, page2.Count);
        Assert.NotEqual(page1[0].Id, page2[0].Id);
    }

    [Fact]
    public async Task GetPagedAsync_FilterByTitle_IsCaseInsensitiveContains()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEvent("Лекция про C#", DateTime.UtcNow, DateTime.UtcNow.AddHours(1)));
        await repo.AddAsync(CreateEvent("Концерт", DateTime.UtcNow.AddHours(2), DateTime.UtcNow.AddHours(3)));
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync("КОНЦЕР", null, null, 1, 10);

        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal("Концерт", items[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_FilterByFrom_ReturnsEventsStartingAfter()
    {
        var repo = CreateRepository();
        var now = DateTime.UtcNow;
        await repo.AddAsync(CreateEvent("Раннее", now, now.AddHours(1)));
        await repo.AddAsync(CreateEvent("Позднее", now.AddDays(1), now.AddDays(1).AddHours(1)));
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(null, now.AddDays(1), null, 1, 10);

        Assert.Equal(1, total);
        Assert.Equal("Позднее", items[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_FilterByTo_ReturnsEventsEndingBefore()
    {
        var repo = CreateRepository();
        var now = DateTime.UtcNow;
        await repo.AddAsync(CreateEvent("Раннее", now, now.AddHours(1)));
        await repo.AddAsync(CreateEvent("Позднее", now.AddDays(1), now.AddDays(1).AddHours(1)));
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(null, null, now.AddHours(2), 1, 10);

        Assert.Equal(1, total);
        Assert.Equal("Раннее", items[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_CombinedFilters_ApplyAll()
    {
        var repo = CreateRepository();
        var now = DateTime.UtcNow;
        await repo.AddAsync(CreateEvent("Утренний концерт", now, now.AddHours(1)));
        await repo.AddAsync(CreateEvent("Вечерний концерт", now.AddDays(2), now.AddDays(2).AddHours(1)));
        await repo.AddAsync(CreateEvent("Лекция", now.AddDays(1), now.AddDays(1).AddHours(1)));
        await repo.SaveChangesAsync();

        // title содержит "концерт" И from после сегодня
        var (items, total) = await repo.GetPagedAsync("концерт", now.AddDays(1), null, 1, 10);

        Assert.Equal(1, total);
        Assert.Equal("Вечерний концерт", items[0].Title);
    }

    private static readonly DateTime BaseTime = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPagedAsync_FromAndTo_ReturnsOnlyEventsInsideRange()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEvent("A", BaseTime, BaseTime.AddHours(1)));
        await repo.AddAsync(CreateEvent("B", BaseTime.AddDays(1), BaseTime.AddDays(1).AddHours(1)));
        await repo.AddAsync(CreateEvent("C", BaseTime.AddDays(2), BaseTime.AddDays(2).AddHours(1)));
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(
            null, BaseTime.AddDays(1), BaseTime.AddDays(1).AddHours(1), 1, 10);

        Assert.Equal(1, total);
        Assert.Equal("B", items[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_TitleAndTo_ApplyBoth()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEvent("Концерт утро", BaseTime, BaseTime.AddHours(1)));
        await repo.AddAsync(CreateEvent("Концерт вечер", BaseTime.AddDays(2), BaseTime.AddDays(2).AddHours(1)));
        await repo.AddAsync(CreateEvent("Лекция", BaseTime.AddHours(3), BaseTime.AddHours(4)));
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(
            "концерт", null, BaseTime.AddDays(1), 1, 10);

        Assert.Equal(1, total);
        Assert.Equal("Концерт утро", items[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_AllFilters_ApplyAll()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEvent("Концерт 1", BaseTime, BaseTime.AddHours(1)));
        await repo.AddAsync(CreateEvent("Концерт 2", BaseTime.AddDays(1), BaseTime.AddDays(1).AddHours(1)));
        await repo.AddAsync(CreateEvent("Концерт 3", BaseTime.AddDays(3), BaseTime.AddDays(3).AddHours(1)));
        await repo.AddAsync(CreateEvent("Лекция", BaseTime.AddDays(1), BaseTime.AddDays(1).AddHours(1)));
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(
            "концерт", BaseTime.AddHours(12), BaseTime.AddDays(2), 1, 10);

        Assert.Equal(1, total);
        Assert.Equal("Концерт 2", items[0].Title);
    }

    [Fact]
    public async Task GetPagedAsync_NoMatches_ReturnsEmptyResult()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEvent("Концерт", BaseTime, BaseTime.AddHours(1)));
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync("несуществующее", null, null, 1, 10);

        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetPagedAsync_PageBeyondRange_ReturnsEmptyItemsButKeepsTotal()
    {
        var repo = CreateRepository();
        for (var i = 0; i < 3; i++)
        {
            await repo.AddAsync(CreateEvent($"Событие {i}",
                BaseTime.AddHours(i), BaseTime.AddHours(i + 1)));
        }
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(null, null, null, 5, 2);

        Assert.Equal(3, total);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetPagedAsync_LastPage_ReturnsRemainingItems()
    {
        var repo = CreateRepository();
        for (var i = 0; i < 5; i++)
        {
            await repo.AddAsync(CreateEvent($"Событие {i}",
                BaseTime.AddHours(i), BaseTime.AddHours(i + 1)));
        }
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(null, null, null, 3, 2);

        Assert.Equal(5, total);
        Assert.Single(items);
    }

    [Fact]
    public async Task GetPagedAsync_FromAndToBoundaries_AreInclusive()
    {
        var repo = CreateRepository();
        await repo.AddAsync(CreateEvent("Граница", BaseTime, BaseTime.AddHours(2)));
        await repo.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(null, BaseTime, BaseTime.AddHours(2), 1, 10);

        Assert.Equal(1, total);
        Assert.Single(items);
    }
}