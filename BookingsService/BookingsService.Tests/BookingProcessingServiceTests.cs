using BookingsService.Application.Services;
using BookingsService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task ProcessBookingAsync_PublishesBookingConfirmed_WithBookingData()
    {
        using var scope = _sp.CreateScope();
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var booking = await Bookings(scope).CreateBookingAsync(eventId, userId);

        await Processing(scope).ProcessBookingAsync(booking.Id);

        var message = Assert.Single(_publisher.Published);
        Assert.Equal(booking.Id, message.BookingId);
        Assert.Equal(eventId, message.EventId);
        Assert.Equal(userId, message.UserId);
        Assert.Equal(1, message.SeatsCount);
        Assert.True(message.ConfirmedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task ProcessBookingAsync_KeepsBookingConfirmed_WhenPublishFails()
    {
        using var scope = _sp.CreateScope();
        var booking = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid());
        _publisher.ExceptionToThrow = new InvalidOperationException("broker is down");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Processing(scope).ProcessBookingAsync(booking.Id));

        var after = await Bookings(scope).GetBookingByIdAsync(booking.Id, Guid.NewGuid(), isAdmin: true);
        Assert.Equal(BookingStatus.Confirmed, after!.Status);
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
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task ProcessBookingAsync_Skips_UnknownBooking()
    {
        using var scope = _sp.CreateScope();

        var result = await Processing(scope).ProcessBookingAsync(Guid.NewGuid());

        Assert.Equal(BookingProcessingResult.Skipped, result);
        Assert.Empty(_publisher.Published);
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
        Assert.Empty(_publisher.Published);
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
