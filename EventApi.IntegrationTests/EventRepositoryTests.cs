using EventsApi.Models;
using EventsApi.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventApi.IntegrationTests;

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
}