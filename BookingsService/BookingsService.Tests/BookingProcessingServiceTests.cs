using BookingsService.Application.Services;
using BookingsService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingsService.Tests;

public class BookingProcessingServiceTests
{
    private readonly ServiceProvider _sp = TestServices.Create();

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
    public async Task ProcessBookingAsync_SkipsCancelledBooking()
    {
        using var scope = _sp.CreateScope();
        var owner = Guid.NewGuid();
        var booking = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), owner);
        await Bookings(scope).CancelBookingAsync(booking.Id, owner, isAdmin: false);

        var result = await Processing(scope).ProcessBookingAsync(booking.Id);

        var after = await Bookings(scope).GetBookingByIdAsync(booking.Id, Guid.NewGuid(), isAdmin: true);
        Assert.Equal(BookingProcessingResult.Skipped, result);
        Assert.Equal(BookingStatus.Cancelled, after!.Status);
    }

    [Fact]
    public async Task ProcessBookingAsync_Skips_UnknownBooking()
    {
        using var scope = _sp.CreateScope();

        var result = await Processing(scope).ProcessBookingAsync(Guid.NewGuid());

        Assert.Equal(BookingProcessingResult.Skipped, result);
    }

    [Fact]
    public async Task RejectAsync_RejectsPendingBooking()
    {
        using var scope = _sp.CreateScope();
        var booking = await Bookings(scope).CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid());

        var rejected = await Processing(scope).RejectAsync(booking.Id);

        var after = await Bookings(scope).GetBookingByIdAsync(booking.Id, Guid.NewGuid(), isAdmin: true);
        Assert.True(rejected);
        Assert.Equal(BookingStatus.Rejected, after!.Status);
        Assert.NotNull(after.ProcessedAt);
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
