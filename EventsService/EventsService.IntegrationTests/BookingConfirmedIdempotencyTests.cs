using EventServiceManager.Contracts;
using EventsService.Application;
using EventsService.Application.Services;
using EventsService.Domain.Entities;
using EventsService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsService.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class BookingConfirmedIdempotencyTests : IntegrationTestBase
{
    public BookingConfirmedIdempotencyTests(PostgresTestContainer fixture) : base(fixture) { }

    private async Task<Event> SeedEventAsync(int seats = 10)
    {
        var repo = new EventRepository(DbContext);
        var ev = Event.Create(Guid.NewGuid(), "Концерт", "desc",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(2), seats);
        await repo.AddAsync(ev);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
        return ev;
    }

    private static BookingConfirmed Message(Guid bookingId, Guid eventId, int seats) =>
        new(BookingId: bookingId,
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

    private async Task<(BookingConfirmedResult? Result, Exception? Error)> TryRunAsync(BookingConfirmed message)
    {
        try { return (await RunAsync(message), null); }
        catch (Exception ex) { return (null, ex); }
    }

    private Task<int> AvailableSeatsAsync(Guid eventId) =>
        DbContext.Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => e.AvailableSeats)
            .SingleAsync();

    private Task<int> ProcessedCountAsync() =>
        DbContext.ProcessedBookings.AsNoTracking().CountAsync();

    [Fact]
    public async Task SameMessageTwice_ReservesSeatsOnce()
    {
        var ev = await SeedEventAsync(10);
        var msg = Message(Guid.NewGuid(), ev.Id, 3);

        var first = await RunAsync(msg);
        var second = await RunAsync(msg);

        Assert.Equal(BookingConfirmedResult.SeatsReserved, first);
        Assert.Equal(BookingConfirmedResult.Duplicate, second);
        Assert.Equal(7, await AvailableSeatsAsync(ev.Id));
        Assert.Equal(1, await ProcessedCountAsync());
    }

    [Fact]
    public async Task ParallelSameMessage_ReservesSeatsOnce()
    {
        var ev = await SeedEventAsync(10);
        var msg = Message(Guid.NewGuid(), ev.Id, 3);

        var outcomes = await Task.WhenAll(TryRunAsync(msg), TryRunAsync(msg));

        Assert.Equal(1, outcomes.Count(o => o.Result == BookingConfirmedResult.SeatsReserved));
        Assert.All(
            outcomes.Where(o => o.Result != BookingConfirmedResult.SeatsReserved),
            o => Assert.True(
                o.Result == BookingConfirmedResult.Duplicate || o.Error is DbUpdateConcurrencyException,
                $"Unexpected: {o.Result} / {o.Error}"));

        Assert.Equal(BookingConfirmedResult.Duplicate, await RunAsync(msg));
        Assert.Equal(7, await AvailableSeatsAsync(ev.Id));
        Assert.Equal(1, await ProcessedCountAsync());
    }

    [Fact]
    public async Task InvalidSeatsCount_IsNotRecorded()
    {
        var ev = await SeedEventAsync(10);

        var result = await RunAsync(Message(Guid.NewGuid(), ev.Id, 0));

        Assert.Equal(BookingConfirmedResult.InvalidMessage, result);
        Assert.Equal(10, await AvailableSeatsAsync(ev.Id));
        Assert.Equal(0, await ProcessedCountAsync());
    }

    [Fact]
    public async Task NoAvailableSeats_IsNotRecorded()
    {
        var ev = await SeedEventAsync(2);

        var result = await RunAsync(Message(Guid.NewGuid(), ev.Id, 5));

        Assert.Equal(BookingConfirmedResult.NoAvailableSeats, result);
        Assert.Equal(2, await AvailableSeatsAsync(ev.Id));
        Assert.Equal(0, await ProcessedCountAsync());
    }
}