using BookingsService.Application.Services;
using BookingsService.Domain.Entities;
using BookingsService.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingsService.Tests;

public class BookingRulesTests
{
    private const int ActiveBookingsLimit = 10;

    private readonly ServiceProvider _sp = TestServices.Create();

    private static IBookingService Svc(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IBookingService>();

    [Fact]
    public async Task CreateBookingAsync_Throws_OnEleventhActiveBooking()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        for (var i = 0; i < ActiveBookingsLimit; i++)
            await svc.CreateBookingAsync(eventId, userId);

        await Assert.ThrowsAsync<ActiveBookingsLimitExceededException>(
            () => svc.CreateBookingAsync(eventId, userId));
    }

    [Fact]
    public async Task CreateBookingAsync_LimitIsPerUser()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var eventId = Guid.NewGuid();
        var firstUser = Guid.NewGuid();

        for (var i = 0; i < ActiveBookingsLimit; i++)
            await svc.CreateBookingAsync(eventId, firstUser);

        var booking = await svc.CreateBookingAsync(eventId, Guid.NewGuid());

        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_LimitIsAcrossDifferentEvents()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var userId = Guid.NewGuid();

        for (var i = 0; i < ActiveBookingsLimit; i++)
            await svc.CreateBookingAsync(Guid.NewGuid(), userId);

        await Assert.ThrowsAsync<ActiveBookingsLimitExceededException>(
            () => svc.CreateBookingAsync(Guid.NewGuid(), userId));
    }

    [Fact]
    public async Task CreateBookingAsync_RejectedBookingDoesNotCountTowardLimit()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = await svc.CreateBookingAsync(eventId, userId);
        for (var i = 1; i < ActiveBookingsLimit; i++)
            await svc.CreateBookingAsync(eventId, userId);

        await svc.UpdateBookingStatusAsync(
            first.Id, BookingStatus.Rejected, DateTime.UtcNow, CancellationToken.None);

        var extra = await svc.CreateBookingAsync(eventId, userId);

        Assert.Equal(BookingStatus.Pending, extra.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_ConfirmedBookingCountsTowardLimit()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = await svc.CreateBookingAsync(eventId, userId);
        for (var i = 1; i < ActiveBookingsLimit; i++)
            await svc.CreateBookingAsync(eventId, userId);

        await svc.UpdateBookingStatusAsync(
            first.Id, BookingStatus.Confirmed, DateTime.UtcNow, CancellationToken.None);

        await Assert.ThrowsAsync<ActiveBookingsLimitExceededException>(
            () => svc.CreateBookingAsync(eventId, userId));
    }
}
