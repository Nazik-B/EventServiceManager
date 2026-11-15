using BookingsService.Domain.Entities;
using BookingsService.Domain.Exceptions;
using BookingsService.Infrastructure.DataAccess;
using BookingsService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingsService.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class BookingRepositoryTests : IntegrationTestBase
{
    public BookingRepositoryTests(PostgresTestContainer fixture) : base(fixture) { }

    private BookingRepository CreateRepository() => new(DbContext);

    private static Booking CreateBooking(Guid? eventId = null, Guid? userId = null) =>
        Booking.Create(eventId ?? Guid.NewGuid(), userId ?? Guid.NewGuid());

    [Fact]
    public async Task AddAsync_SavesBooking_WithoutExistingEventOrUser()
    {
        var repo = CreateRepository();
        var booking = CreateBooking();

        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var fromDb = await DbContext.Bookings.FindAsync(booking.Id);
        Assert.NotNull(fromDb);
        Assert.Equal(BookingStatus.Pending, fromDb.Status);
        Assert.Equal(booking.EventId, fromDb.EventId);
        Assert.Equal(booking.UserId, fromDb.UserId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsBooking_WhenExists()
    {
        var repo = CreateRepository();
        var booking = CreateBooking();
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await repo.GetByIdAsync(booking.Id);

        Assert.NotNull(result);
        Assert.Equal(booking.Id, result.Id);
        Assert.Equal(booking.EventId, result.EventId);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = CreateRepository();

        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByIdTrackedAsync_ReturnsTrackedEntity()
    {
        var repo = CreateRepository();
        var booking = CreateBooking();
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await repo.GetByIdTrackedAsync(booking.Id);

        Assert.NotNull(result);
        Assert.Equal(EntityState.Unchanged, DbContext.Entry(result!).State);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllOrderedByCreatedDesc()
    {
        var repo = CreateRepository();
        var b1 = CreateBooking();
        await Task.Delay(50);
        var b2 = CreateBooking();
        await repo.AddAsync(b1);
        await repo.AddAsync(b2);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = (await repo.GetAllAsync()).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(b2.Id, result[0].Id);
        Assert.Equal(b1.Id, result[1].Id);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesBookingStatus()
    {
        var repo = CreateRepository();
        var booking = CreateBooking();
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetByIdTrackedAsync(booking.Id);
        Assert.NotNull(tracked);
        tracked.Status = BookingStatus.Confirmed;
        tracked.ProcessedAt = DateTime.UtcNow;
        await repo.UpdateAsync(tracked);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var updated = await DbContext.Bookings.FindAsync(booking.Id);
        Assert.NotNull(updated);
        Assert.Equal(BookingStatus.Confirmed, updated.Status);
        Assert.NotNull(updated.ProcessedAt);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBooking()
    {
        var repo = CreateRepository();
        var booking = CreateBooking();
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();

        var tracked = await repo.GetByIdTrackedAsync(booking.Id);
        await repo.DeleteAsync(tracked!);
        await repo.SaveChangesAsync();

        Assert.False(await DbContext.Bookings.AnyAsync(b => b.Id == booking.Id));
    }

    [Fact]
    public async Task ExistsAsync_ReturnsCorrectFlag()
    {
        var repo = CreateRepository();
        var booking = CreateBooking();
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();

        Assert.True(await repo.ExistsAsync(booking.Id));
        Assert.False(await repo.ExistsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPending()
    {
        var repo = CreateRepository();
        var pending = CreateBooking();
        var confirmed = CreateBooking();
        confirmed.Status = BookingStatus.Confirmed;
        var rejected = CreateBooking();
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
        var pending = CreateBooking();
        var confirmed = CreateBooking();
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
        var event1 = Guid.NewGuid();
        var event2 = Guid.NewGuid();
        await repo.AddAsync(CreateBooking(event1));
        await repo.AddAsync(CreateBooking(event1));
        await repo.AddAsync(CreateBooking(event2));
        await repo.SaveChangesAsync();

        var result = await repo.GetByEventIdAsync(event1);

        Assert.Equal(2, result.Count);
        Assert.All(result, b => Assert.Equal(event1, b.EventId));
    }

    [Fact]
    public async Task CountActiveByUserAsync_CountsOnlyPendingAndConfirmedOfThatUser()
    {
        var repo = CreateRepository();
        var user = Guid.NewGuid();

        var pending = CreateBooking(userId: user);
        var confirmed = CreateBooking(userId: user);
        confirmed.Status = BookingStatus.Confirmed;
        var rejected = CreateBooking(userId: user);
        rejected.Status = BookingStatus.Rejected;
        var cancelled = CreateBooking(userId: user);
        cancelled.Status = BookingStatus.Cancelled;
        var otherUsers = CreateBooking();

        foreach (var b in new[] { pending, confirmed, rejected, cancelled, otherUsers })
            await repo.AddAsync(b);
        await repo.SaveChangesAsync();

        var count = await repo.CountActiveByUserAsync(user);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task SaveChangesAsync_ThrowsConcurrencyConflict_OnStaleUpdate()
    {
        var repo = CreateRepository();
        var booking = CreateBooking();
        await repo.AddAsync(booking);
        await repo.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var stale = await repo.GetByIdTrackedAsync(booking.Id);

        using var scope = Services.CreateScope();
        var other = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var fresh = await other.Bookings.FirstAsync(b => b.Id == booking.Id);
        fresh.Status = BookingStatus.Confirmed;
        await other.SaveChangesAsync();

        stale!.Status = BookingStatus.Cancelled;

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repo.SaveChangesAsync());
    }
}
