using EventsApi.Models;
using EventsApi.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventApi.IntegrationTests;

public class BookingRepositoryTests : IntegrationTestBase
{
    public BookingRepositoryTests(PostgresTestContainer fixture) : base(fixture) { }

    private BookingRepository CreateRepository() => new(DbContext);

    private async Task<Event> SeedEventAsync()
    {
        var ev = Event.Create(Guid.NewGuid(), "Концерт", null,
            DateTime.UtcNow, DateTime.UtcNow.AddHours(2), 10);
        DbContext.Events.Add(ev);
        await DbContext.SaveChangesAsync();
        return ev;
    }

    private static Booking CreateBooking(Guid eventId) => Booking.Create(eventId);

    [Fact]
    public async Task AddAsync_SavesBookingToDatabase()
    {
        var repo = CreateRepository();
        var ev = await SeedEventAsync();
        var booking = CreateBooking(ev.Id);

        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();

        var fromDb = await DbContext.Bookings.FindAsync(booking.Id);
        Assert.NotNull(fromDb);
        Assert.Equal(BookingStatus.Pending, fromDb.Status);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsBooking_WhenExists()
    {
        var repo = CreateRepository();
        var ev = await SeedEventAsync();
        var booking = CreateBooking(ev.Id);
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();

        var result = await repo.GetByIdAsync(booking.Id);

        Assert.NotNull(result);
        Assert.Equal(booking.Id, result.Id);
        Assert.Equal(ev.Id, result.EventId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = CreateRepository();

        var result = await repo.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_ReturnsTrackedEntity()
    {
        var repo = CreateRepository();
        var ev = await SeedEventAsync();
        var booking = CreateBooking(ev.Id);
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await repo.GetByIdForUpdateAsync(booking.Id);

        Assert.NotNull(result);
        Assert.Equal(EntityState.Unchanged, DbContext.Entry(result!).State);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrderedByCreatedDesc()
    {
        var repo = CreateRepository();
        var ev = await SeedEventAsync();
        var b1 = CreateBooking(ev.Id);
        var b2 = CreateBooking(ev.Id);
        await repo.AddAsync(b1);
        await repo.AddAsync(b2);
        await repo.SaveChangesAsync();

        var result = await repo.GetAllAsync();

        Assert.Equal(2, result.Count());
        Assert.Equal(b2.Id, result.First().Id);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesBookingStatus()
    {
        var repo = CreateRepository();
        var ev = await SeedEventAsync();
        var booking = CreateBooking(ev.Id);
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetByIdForUpdateAsync(booking.Id);
        tracked.Status = BookingStatus.Confirmed;
        tracked.ProcessedAt = DateTime.UtcNow;
        await repo.UpdateAsync(tracked!);
        await repo.SaveChangesAsync();

        var updated = await DbContext.Bookings.FindAsync(booking.Id);
        Assert.Equal(BookingStatus.Confirmed, updated.Status);
        Assert.NotNull(updated.ProcessedAt);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBooking()
    {
        var repo = CreateRepository();
        var ev = await SeedEventAsync();
        var booking = CreateBooking(ev.Id);
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetByIdForUpdateAsync(booking.Id);
        await repo.DeleteAsync(tracked!);
        await repo.SaveChangesAsync();

        Assert.False(await DbContext.Bookings.AnyAsync(b => b.Id == booking.Id));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsCorrectFlag()
    {
        var repo = CreateRepository();
        var ev = await SeedEventAsync();
        var booking = CreateBooking(ev.Id);
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();

        Assert.True(await repo.ExistsAsync(booking.Id));
        Assert.False(await repo.ExistsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPending()
    {
        var repo = CreateRepository();
        var ev = await SeedEventAsync();

        var pending = CreateBooking(ev.Id);
        var confirmed = CreateBooking(ev.Id);
        confirmed.Status = BookingStatus.Confirmed;
        var rejected = CreateBooking(ev.Id);
        rejected.Status = BookingStatus.Rejected;

        await repo.AddAsync(pending);
        await repo.AddAsync(confirmed);
        await repo.AddAsync(rejected);
        await repo.SaveChangesAsync();

        var result = await repo.GetPendingAsync();

        Assert.Single(result);
        Assert.Equal(BookingStatus.Pending, result[0].Status);
    }

    [Fact]
    public async Task GetPendingIdsAsync_ReturnsOnlyPendingIds()
    {
        var repo = CreateRepository();
        var ev = await SeedEventAsync();

        var pending = CreateBooking(ev.Id);
        var confirmed = CreateBooking(ev.Id);
        confirmed.Status = BookingStatus.Confirmed;

        await repo.AddAsync(pending);
        await repo.AddAsync(confirmed);
        await repo.SaveChangesAsync();

        var result = await repo.GetPendingIdsAsync();

        Assert.Single(result);
        Assert.Equal(pending.Id, result[0]);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsBookingsForEvent()
    {
        var repo = CreateRepository();
        var ev1 = await SeedEventAsync();
        var ev2 = await SeedEventAsync();

        await repo.AddAsync(CreateBooking(ev1.Id));
        await repo.AddAsync(CreateBooking(ev1.Id));
        await repo.AddAsync(CreateBooking(ev2.Id));
        await repo.SaveChangesAsync();

        var result = await repo.GetByEventIdAsync(ev1.Id);

        Assert.Equal(2, result.Count());
        Assert.All(result, b => Assert.Equal(ev1.Id, b.EventId));
    }
}