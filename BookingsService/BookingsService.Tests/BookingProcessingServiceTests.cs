using System.Text.Json;
using BookingsService.Application.Services;
using BookingsService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using BookingsService.Infrastructure.DataAccess;
using EventServiceManager.Contracts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BookingsService.Tests;

public class BookingProcessingServiceTests
{
    private readonly FakeBookingEventPublisher _publisher = new();
    private readonly ServiceProvider _sp;

    public BookingProcessingServiceTests()
    {
        _sp = TestServices.Create(_publisher);
    }

    private static IBookingService Bookings(IServiceScope s) =>
        s.ServiceProvider.GetRequiredService<IBookingService>();

    private static IBookingProcessingService Processing(IServiceScope s) =>
        s.ServiceProvider.GetRequiredService<IBookingProcessingService>();

    private static Task<List<OutboxMessage>> Outbox(IServiceScope s) =>
        s.ServiceProvider.GetRequiredService<BookingsDbContext>().OutboxMessages.AsNoTracking().ToListAsync();

    [Fact]
    public async Task GetPendingBookingIdsAsync_ReturnsOnlyPending()
    {
        using var scope = _sp.CreateScope();
        var owner = Guid.NewGuid();
        var pending = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), owner);
        var cancelled = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), owner);
        await Bookings(scope).CancelBookingAsync(cancelled.Id, owner, isAdmin: false);

        var ids = await Processing(scope).GetPendingBookingIdsAsync();

        Assert.Contains(pending.Id, ids);
        Assert.DoesNotContain(cancelled.Id, ids);
    }

    [Fact]
    public async Task ProcessBookingAsync_ConfirmsPendingBooking()
    {
        using var scope = _sp.CreateScope();
        var booking = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid());

        var result = await Processing(scope).ProcessBookingAsync(booking.Id);

        var after = await Bookings(scope).GetBookingByIdAsync(booking.Id, Guid.NewGuid(), isAdmin: true);
        Assert.Equal(BookingProcessingResult.Confirmed, result);
        Assert.Equal(BookingStatus.Confirmed, after!.Status);
        Assert.NotNull(after.ProcessedAt);
    }

    [Fact]
    public async Task ProcessBookingAsync_StoresBookingConfirmedInOutbox_WithBookingData()
    {
        using var scope = _sp.CreateScope();
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var booking = await Bookings(scope).CreateBookingAsync(eventId, userId);

        await Processing(scope).ProcessBookingAsync(booking.Id);

        var row = Assert.Single(await Outbox(scope));
        Assert.Equal(nameof(BookingConfirmed), row.Type);
        Assert.Equal(KafkaTopics.BookingConfirmed, row.Topic);
        Assert.Equal(eventId.ToString(), row.Key);
        Assert.Null(row.ProcessedAt);
        var message = JsonSerializer.Deserialize<BookingConfirmed>(row.Payload)!;
        Assert.Equal(booking.Id, message.BookingId);
        Assert.Equal(eventId, message.EventId);
        Assert.Equal(userId, message.UserId);
        Assert.Equal(1, message.SeatsCount);
        Assert.True(message.ConfirmedAt <= DateTime.UtcNow);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task ProcessBookingAsync_ConfirmsBooking_AndStoresOutbox_EvenIfBrokerIsDown()
    {
        using var scope = _sp.CreateScope();
        var booking = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid());
        _publisher.ExceptionToThrow = new InvalidOperationException("broker is down");

        var result = await Processing(scope).ProcessBookingAsync(booking.Id);

        var after = await Bookings(scope).GetBookingByIdAsync(booking.Id, Guid.NewGuid(), isAdmin: true);
        Assert.Equal(BookingProcessingResult.Confirmed, result);
        Assert.Equal(BookingStatus.Confirmed, after!.Status);
        var row = Assert.Single(await Outbox(scope));
        Assert.Null(row.ProcessedAt);
    }

    [Fact]
    public async Task ProcessBookingAsync_SkipsCancelledBooking_AndPublishesNothing()
    {
        using var scope = _sp.CreateScope();
        var owner = Guid.NewGuid();
        var booking = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), owner);
        await Bookings(scope).CancelBookingAsync(booking.Id, owner, isAdmin: false);

        var result = await Processing(scope).ProcessBookingAsync(booking.Id);

        var after = await Bookings(scope).GetBookingByIdAsync(booking.Id, Guid.NewGuid(), isAdmin: true);
        Assert.Equal(BookingProcessingResult.Skipped, result);
        Assert.Equal(BookingStatus.Cancelled, after!.Status);
        Assert.Empty(await Outbox(scope));
    }

    [Fact]
    public async Task ProcessBookingAsync_Skips_UnknownBooking()
    {
        using var scope = _sp.CreateScope();

        var result = await Processing(scope).ProcessBookingAsync(Guid.NewGuid());

        Assert.Equal(BookingProcessingResult.Skipped, result);
        Assert.Empty(await Outbox(scope));
    }

    [Fact]
    public async Task RejectAsync_RejectsPendingBooking_AndPublishesNothing()
    {
        using var scope = _sp.CreateScope();
        var booking = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid());

        var rejected = await Processing(scope).RejectAsync(booking.Id);

        var after = await Bookings(scope).GetBookingByIdAsync(booking.Id, Guid.NewGuid(), isAdmin: true);
        Assert.True(rejected);
        Assert.Equal(BookingStatus.Rejected, after!.Status);
        Assert.NotNull(after.ProcessedAt);
        Assert.Empty(await Outbox(scope));
    }

    [Fact]
    public async Task RejectAsync_ReturnsFalse_ForNonPendingBooking()
    {
        using var scope = _sp.CreateScope();
        var booking = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid());
        await Processing(scope).ProcessBookingAsync(booking.Id);

        var rejected = await Processing(scope).RejectAsync(booking.Id);

        Assert.False(rejected);
    }
}
