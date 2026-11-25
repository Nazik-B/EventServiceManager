using EventsService.Application.Caching;
using EventsService.Application.Dto;
using EventsService.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsService.Tests;

public class EventServiceCacheTests
{
    private readonly ServiceProvider _provider = TestDbContextFactory.CreateServiceProvider();

    private InMemoryCacheService Cache => _provider.GetRequiredService<InMemoryCacheService>();

    private static IEventService Service(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IEventService>();

    private static Task<EventResponse> CreateEventAsync(IEventService service, string title, int dayOffset = 0, int seats = 10)
    {
        var start = new DateTime(2027, 1, 1).AddDays(dayOffset);
        return service.CreateAsync(new CreateEventRequest
        {
            Title = title,
            StartAt = start,
            EndAt = start.AddHours(2),
            TotalSeats = seats
        });
    }

    private void SeedBoth(Guid id)
    {
        Cache.Seed(CacheKeys.Event(id), new object());
        Cache.Seed(CacheKeys.TopEvents, new object());
    }

    private void AssertEventRemovedTopKept(Guid id)
    {
        Assert.False(Cache.Contains(CacheKeys.Event(id)));
        Assert.True(Cache.Contains(CacheKeys.TopEvents));
    }

    private void AssertBothKept(Guid id)
    {
        Assert.True(Cache.Contains(CacheKeys.Event(id)));
        Assert.True(Cache.Contains(CacheKeys.TopEvents));
    }

    [Fact]
    public async Task GetByIdAsync_OnMiss_LoadsFromDbAndStoresWithEventTtl()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var created = await CreateEventAsync(service, "Concert");
        Cache.ClearCalls();

        var result = await service.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal("Concert", result!.Title);
        Assert.True(Cache.Contains(CacheKeys.Event(created.Id)));
        var set = Assert.Single(Cache.SetCalls);
        Assert.Equal(CacheKeys.Event(created.Id), set.Key);
        Assert.Equal(TimeSpan.FromSeconds(new CacheOptions().EventTtlSeconds), set.Ttl);
    }

    [Fact]
    public async Task GetByIdAsync_OnHit_ReturnsCachedValueWithoutTouchingDb()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var id = Guid.NewGuid();
        Cache.Seed(CacheKeys.Event(id), new EventResponse { Id = id, Title = "From cache" });

        var result = await service.GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal("From cache", result!.Title);
        Assert.Empty(Cache.SetCalls);
    }

    [Fact]
    public async Task GetByIdAsync_WhenEventNotFound_ReturnsNullAndDoesNotCache()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var id = Guid.NewGuid();

        var result = await service.GetByIdAsync(id);

        Assert.Null(result);
        Assert.Empty(Cache.SetCalls);
        Assert.False(Cache.Contains(CacheKeys.Event(id)));
    }

    [Fact]
    public async Task GetTopAsync_OnMiss_ReturnsTenMostSoldEventsAndCachesThem()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);

        var ids = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            ids.Add((await CreateEventAsync(service, $"Event {i}", i)).Id);
        }

        for (var i = 0; i < 10; i++)
        {
            for (var seat = 0; seat < i; seat++)
            {
                Assert.True(await service.TryReserveSeatAsync(ids[i]));
            }
        }

        Cache.ClearCalls();

        var top = await service.GetTopAsync();

        Assert.Equal(10, top.Count);
        Assert.Equal(ids[9], top[0].Id);
        Assert.Equal(ids[1], top[8].Id);
        Assert.Equal(ids[0], top[9].Id);

        Assert.True(Cache.Contains(CacheKeys.TopEvents));
        var set = Assert.Single(Cache.SetCalls);
        Assert.Equal(CacheKeys.TopEvents, set.Key);
        Assert.Equal(TimeSpan.FromSeconds(new CacheOptions().TopEventsTtlSeconds), set.Ttl);
    }

    [Fact]
    public async Task GetTopAsync_OnHit_ReturnsCachedListWithoutTouchingDb()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        Cache.Seed(CacheKeys.TopEvents, new List<EventResponse>
        {
            new() { Id = Guid.NewGuid(), Title = "Cached top" }
        });

        var top = await service.GetTopAsync();

        var item = Assert.Single(top);
        Assert.Equal("Cached top", item.Title);
        Assert.Empty(Cache.SetCalls);
    }

    [Fact]
    public async Task GetTopAsync_WhenNoEvents_CachesEmptyList()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);

        var top = await service.GetTopAsync();

        Assert.Empty(top);
        Assert.True(Cache.Contains(CacheKeys.TopEvents));
    }

    [Fact]
    public async Task CreateAsync_DoesNotTouchTopCache()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        Cache.Seed(CacheKeys.TopEvents, new object());

        await CreateEventAsync(service, "New");

        Assert.True(Cache.Contains(CacheKeys.TopEvents));
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesEventCacheOnly()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var created = await CreateEventAsync(service, "Old");
        SeedBoth(created.Id);

        var updated = await service.UpdateAsync(created.Id, new UpdateEventRequest
        {
            Title = "Updated",
            StartAt = new DateTime(2027, 2, 1),
            EndAt = new DateTime(2027, 2, 1).AddHours(2)
        });

        Assert.True(updated);
        AssertEventRemovedTopKept(created.Id);
    }

    [Fact]
    public async Task UpdateAsync_WhenEventNotFound_KeepsCache()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var id = Guid.NewGuid();
        SeedBoth(id);

        var updated = await service.UpdateAsync(id, new UpdateEventRequest
        {
            Title = "Updated",
            StartAt = new DateTime(2027, 2, 1),
            EndAt = new DateTime(2027, 2, 1).AddHours(2)
        });

        Assert.False(updated);
        AssertBothKept(id);
    }

    [Fact]
    public async Task GetByIdAsync_AfterUpdate_ReturnsFreshData()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var created = await CreateEventAsync(service, "Before");

        Assert.Equal("Before", (await service.GetByIdAsync(created.Id))!.Title);

        await service.UpdateAsync(created.Id, new UpdateEventRequest
        {
            Title = "After",
            StartAt = new DateTime(2027, 2, 1),
            EndAt = new DateTime(2027, 2, 1).AddHours(2)
        });

        Assert.Equal("After", (await service.GetByIdAsync(created.Id))!.Title);
    }

    [Fact]
    public async Task DeleteAsync_InvalidatesEventCacheOnly()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var created = await CreateEventAsync(service, "ToDelete");
        SeedBoth(created.Id);

        var deleted = await service.DeleteAsync(created.Id);

        Assert.True(deleted);
        AssertEventRemovedTopKept(created.Id);
    }

    [Fact]
    public async Task TryReserveSeatAsync_WhenReserved_InvalidatesEventCacheOnly()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var created = await CreateEventAsync(service, "Reserve");
        SeedBoth(created.Id);

        Assert.True(await service.TryReserveSeatAsync(created.Id));

        AssertEventRemovedTopKept(created.Id);
    }

    [Fact]
    public async Task TryReserveSeatAsync_WhenNoSeatsLeft_KeepsCache()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var created = await CreateEventAsync(service, "Single seat", seats: 1);
        Assert.True(await service.TryReserveSeatAsync(created.Id));
        SeedBoth(created.Id);

        Assert.False(await service.TryReserveSeatAsync(created.Id));

        AssertBothKept(created.Id);
    }

    [Fact]
    public async Task ReleaseSeatAsync_InvalidatesEventCacheOnly()
    {
        using var scope = _provider.CreateScope();
        var service = Service(scope);
        var created = await CreateEventAsync(service, "Release");
        Assert.True(await service.TryReserveSeatAsync(created.Id));
        SeedBoth(created.Id);

        Assert.True(await service.ReleaseSeatAsync(created.Id));

        AssertEventRemovedTopKept(created.Id);
    }
}