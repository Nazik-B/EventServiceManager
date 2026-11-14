using EventsApi.Application.Dto;
using EventsApi.Application.Services;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.UnitTests;

public class BookingCancellationTests
{
    private readonly ServiceProvider _serviceProvider = TestDbContextFactory.CreateServiceProvider();

    private static CreateEventRequest BuildRequest(int totalSeats = 3) => new()
    {
        Title = "Cancel Event",
        StartAt = DateTime.UtcNow.AddDays(30),
        EndAt = DateTime.UtcNow.AddDays(30).AddHours(1),
        TotalSeats = totalSeats
    };

    private async Task<(IServiceScope Scope, IBookingService Bookings, IEventService Events, EventResponse Event)> ArrangeAsync(int totalSeats = 3)
    {
        var scope = _serviceProvider.CreateScope();
        var events = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var ev = await events.CreateAsync(BuildRequest(totalSeats));
        return (scope, bookings, events, ev);
    }

    [Fact]
    public async Task CancelBookingAsync_CancelsPendingBooking_AndReleasesSeat()
    {
        var (scope, bookings, events, ev) = await ArrangeAsync(totalSeats: 3);
        using var _ = scope;
        var owner = Guid.NewGuid();

        var booking = await bookings.CreateBookingAsync(ev.Id, owner);
        var cancelled = await bookings.CancelBookingAsync(booking.Id, owner, isAdmin: false);

        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        var after = await events.GetByIdAsync(ev.Id);
        Assert.Equal(3, after!.AvailableSeats);
    }

    [Fact]
    public async Task CancelBookingAsync_CancelsConfirmedBooking()
    {
        var (scope, bookings, events, ev) = await ArrangeAsync();
        using var _ = scope;
        var owner = Guid.NewGuid();

        var booking = await bookings.CreateBookingAsync(ev.Id, owner);
        await bookings.UpdateBookingStatusAsync(
            booking.Id, BookingStatus.Confirmed, DateTime.UtcNow, CancellationToken.None);

        var cancelled = await bookings.CancelBookingAsync(booking.Id, owner, isAdmin: false);

        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_ThrowsForbidden_WhenUserIsNotOwner()
    {
        var (scope, bookings, events, ev) = await ArrangeAsync();
        using var _ = scope;

        var booking = await bookings.CreateBookingAsync(ev.Id, Guid.NewGuid());

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            bookings.CancelBookingAsync(booking.Id, Guid.NewGuid(), isAdmin: false));

        var after = await events.GetByIdAsync(ev.Id);
        Assert.Equal(2, after!.AvailableSeats);
    }

    [Fact]
    public async Task CancelBookingAsync_AllowsAdmin_ToCancelAnyBooking()
    {
        var (scope, bookings, events, ev) = await ArrangeAsync();
        using var _ = scope;

        var booking = await bookings.CreateBookingAsync(ev.Id, Guid.NewGuid());
        var cancelled = await bookings.CancelBookingAsync(booking.Id, Guid.NewGuid(), isAdmin: true);

        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_ThrowsNotFound_ForUnknownBooking()
    {
        var (scope, bookings, _, _) = await ArrangeAsync();
        using var _ = scope;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            bookings.CancelBookingAsync(Guid.NewGuid(), Guid.NewGuid(), isAdmin: true));
    }

    [Fact]
    public async Task CancelBookingAsync_ThrowsValidation_WhenAlreadyCancelled_AndDoesNotReleaseSeatTwice()
    {
        var (scope, bookings, events, ev) = await ArrangeAsync(totalSeats: 3);
        using var _ = scope;
        var owner = Guid.NewGuid();

        var booking = await bookings.CreateBookingAsync(ev.Id, owner);
        await bookings.CancelBookingAsync(booking.Id, owner, isAdmin: false);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            bookings.CancelBookingAsync(booking.Id, owner, isAdmin: false));

        var after = await events.GetByIdAsync(ev.Id);
        Assert.Equal(3, after!.AvailableSeats);
    }

    [Fact]
    public async Task CancelBookingAsync_ThrowsValidation_ForRejectedBooking()
    {
        var (scope, bookings, _, ev) = await ArrangeAsync();
        using var _ = scope;
        var owner = Guid.NewGuid();

        var booking = await bookings.CreateBookingAsync(ev.Id, owner);
        await bookings.UpdateBookingStatusAsync(
            booking.Id, BookingStatus.Rejected, DateTime.UtcNow, CancellationToken.None);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            bookings.CancelBookingAsync(booking.Id, owner, isAdmin: false));
    }

    [Fact]
    public async Task CancelBookingAsync_FreesSlotInActiveBookingsLimit()
    {
        var (scope, bookings, _, ev) = await ArrangeAsync(totalSeats: 30);
        using var _ = scope;
        var owner = Guid.NewGuid();

        var first = await bookings.CreateBookingAsync(ev.Id, owner);
        for (var i = 1; i < 10; i++)
            await bookings.CreateBookingAsync(ev.Id, owner);

        await bookings.CancelBookingAsync(first.Id, owner, isAdmin: false);

        var extra = await bookings.CreateBookingAsync(ev.Id, owner);
        Assert.Equal(BookingStatus.Pending, extra.Status);
    }

    [Fact]
    public async Task ProcessBookingAsync_SkipsCancelledBooking()
    {
        var (scope, bookings, _, ev) = await ArrangeAsync();
        using var _ = scope;
        var processing = scope.ServiceProvider.GetRequiredService<IBookingProcessingService>();
        var owner = Guid.NewGuid();

        var booking = await bookings.CreateBookingAsync(ev.Id, owner);
        await bookings.CancelBookingAsync(booking.Id, owner, isAdmin: false);

        var result = await processing.ProcessBookingAsync(booking.Id);
        var after = await bookings.GetBookingByIdAsync(booking.Id);

        Assert.Equal(BookingProcessingResult.Skipped, result);
        Assert.Equal(BookingStatus.Cancelled, after!.Status);
    }
}
