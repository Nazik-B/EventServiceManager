using BookingsService.Application.Services;
using BookingsService.Domain.Entities;
using BookingsService.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingsService.Tests;

public class BookingCancellationTests
{
    private readonly ServiceProvider _sp = TestServices.Create();

    private static IBookingService Svc(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IBookingService>();

    [Fact]
    public async Task CancelBookingAsync_CancelsPendingBooking()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var owner = Guid.NewGuid();
        var booking = await svc.CreateBookingAsync(Guid.NewGuid(), owner);

        var cancelled = await svc.CancelBookingAsync(booking.Id, owner, isAdmin: false);

        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_CancelsConfirmedBooking()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var owner = Guid.NewGuid();
        var booking = await svc.CreateBookingAsync(Guid.NewGuid(), owner);
        await svc.UpdateBookingStatusAsync(
            booking.Id, BookingStatus.Confirmed, DateTime.UtcNow, CancellationToken.None);

        var cancelled = await svc.CancelBookingAsync(booking.Id, owner, isAdmin: false);

        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_ThrowsForbidden_WhenUserIsNotOwner()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var booking = await svc.CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenOperationException>(
            () => svc.CancelBookingAsync(booking.Id, Guid.NewGuid(), isAdmin: false));

        var after = await svc.GetBookingByIdAsync(booking.Id, Guid.NewGuid(), isAdmin: true);
        Assert.Equal(BookingStatus.Pending, after!.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_AllowsAdmin_ToCancelAnyBooking()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var booking = await svc.CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid());

        var cancelled = await svc.CancelBookingAsync(booking.Id, Guid.NewGuid(), isAdmin: true);

        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_ThrowsNotFound_ForUnknownBooking()
    {
        using var scope = _sp.CreateScope();

        await Assert.ThrowsAsync<NotFoundException>(
            () => Svc(scope).CancelBookingAsync(Guid.NewGuid(), Guid.NewGuid(), isAdmin: true));
    }

    [Fact]
    public async Task CancelBookingAsync_ThrowsValidation_WhenAlreadyCancelled()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var owner = Guid.NewGuid();
        var booking = await svc.CreateBookingAsync(Guid.NewGuid(), owner);
        await svc.CancelBookingAsync(booking.Id, owner, isAdmin: false);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.CancelBookingAsync(booking.Id, owner, isAdmin: false));
    }

    [Fact]
    public async Task CancelBookingAsync_ThrowsValidation_ForRejectedBooking()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var owner = Guid.NewGuid();
        var booking = await svc.CreateBookingAsync(Guid.NewGuid(), owner);
        await svc.UpdateBookingStatusAsync(
            booking.Id, BookingStatus.Rejected, DateTime.UtcNow, CancellationToken.None);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => svc.CancelBookingAsync(booking.Id, owner, isAdmin: false));
    }

    [Fact]
    public async Task CancelBookingAsync_FreesSlotInActiveBookingsLimit()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var owner = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var first = await svc.CreateBookingAsync(eventId, owner);
        for (var i = 1; i < 10; i++)
            await svc.CreateBookingAsync(eventId, owner);

        await svc.CancelBookingAsync(first.Id, owner, isAdmin: false);

        var extra = await svc.CreateBookingAsync(eventId, owner);
        Assert.Equal(BookingStatus.Pending, extra.Status);
    }
}
