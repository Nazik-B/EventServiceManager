using EventServiceManager.Contracts;
using EventsService.Application;
using EventsService.Application.Caching;
using EventsService.Application.Services;
using EventsService.Domain.Entities;
using EventsService.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsService.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class BookingConfirmedCacheInvalidationTests : IntegrationTestBase
{
    public BookingConfirmedCacheInvalidationTests(PostgresTestContainer fixture) : base(fixture) { }

    private InMemoryCacheService Cache => Services.GetRequiredService<InMemoryCacheService>();

    private async Task<Event> SeedEventAsync(int seats)
    {
        var repo = new EventRepository(DbContext);
        var ev = Event.Create(Guid.NewGuid(), "Concert", "desc",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(2), seats);
        await repo.AddAsync(ev);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        return ev;
    }

    private static BookingConfirmed Message(Guid eventId, int seats) =>
        new(BookingId: Guid.NewGuid(),
            EventId: eventId,
            UserId: Guid.NewGuid(),
            SeatsCount: seats,
            ConfirmedAt: DateTime.UtcNow);

    private async Task<BookingConfirmedResult> RunAsync(BookingConfirmed message)
    {
        await using var scope = Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IBookingConfirmedHandler>();
        return await handler.HandleAsync(message, CancellationToken.None);
    }

    private void SeedCache(Guid eventId)
    {
        Cache.Seed(CacheKeys.Event(eventId), new object());
        Cache.Seed(CacheKeys.TopEvents, new object());
    }

    [Fact]
    public async Task SeatsReserved_InvalidatesEventAndTopCache()
    {
        var ev = await SeedEventAsync(5);
        SeedCache(ev.Id);

        var result = await RunAsync(Message(ev.Id, 2));

        Assert.Equal(BookingConfirmedResult.SeatsReserved, result);
        Assert.False(Cache.Contains(CacheKeys.Event(ev.Id)));
        Assert.False(Cache.Contains(CacheKeys.TopEvents));
    }

    [Fact]
    public async Task Duplicate_LeavesCacheUntouched()
    {
        var ev = await SeedEventAsync(5);
        var message = Message(ev.Id, 1);
        Assert.Equal(BookingConfirmedResult.SeatsReserved, await RunAsync(message));
        SeedCache(ev.Id);

        var result = await RunAsync(message);

        Assert.Equal(BookingConfirmedResult.Duplicate, result);
        Assert.True(Cache.Contains(CacheKeys.Event(ev.Id)));
        Assert.True(Cache.Contains(CacheKeys.TopEvents));
    }

    [Fact]
    public async Task NoAvailableSeats_LeavesCacheUntouched()
    {
        var ev = await SeedEventAsync(1);
        SeedCache(ev.Id);

        var result = await RunAsync(Message(ev.Id, 2));

        Assert.Equal(BookingConfirmedResult.NoAvailableSeats, result);
        Assert.True(Cache.Contains(CacheKeys.Event(ev.Id)));
        Assert.True(Cache.Contains(CacheKeys.TopEvents));
    }
}