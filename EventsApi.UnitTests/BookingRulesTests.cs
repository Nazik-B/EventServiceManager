using EventsApi.Application.Dto;
using EventsApi.Application.Services;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.UnitTests;

public class BookingRulesTests
{
    private const int ActiveBookingsLimit = 10;

    private readonly ServiceProvider _serviceProvider = TestDbContextFactory.CreateServiceProvider();

    private static CreateEventRequest BuildRequest(DateTime start, int totalSeats = 30) => new()
    {
        Title = "Rules Event",
        StartAt = start,
        EndAt = start.AddHours(1),
        TotalSeats = totalSeats
    };

    [Fact]
    public async Task CreateBookingAsync_ThrowsPastEventBookingException_WhenEventAlreadyStarted()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var pastEvent = await eventService.CreateAsync(
            BuildRequest(DateTime.UtcNow.AddDays(-1)));

        await Assert.ThrowsAsync<PastEventBookingException>(() =>
            bookingService.CreateBookingAsync(pastEvent.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateBookingAsync_DoesNotDecreaseSeats_WhenEventAlreadyStarted()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var pastEvent = await eventService.CreateAsync(
            BuildRequest(DateTime.UtcNow.AddDays(-1), totalSeats: 5));

        await Assert.ThrowsAsync<PastEventBookingException>(() =>
            bookingService.CreateBookingAsync(pastEvent.Id, Guid.NewGuid()));

        var after = await eventService.GetByIdAsync(pastEvent.Id);
        Assert.NotNull(after);
        Assert.Equal(5, after!.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_ThrowsActiveBookingsLimitExceeded_OnEleventhActiveBooking()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var ev = await eventService.CreateAsync(BuildRequest(DateTime.UtcNow.AddDays(30)));
        var userId = Guid.NewGuid();

        for (var i = 0; i < ActiveBookingsLimit; i++)
            await bookingService.CreateBookingAsync(ev.Id, userId);

        await Assert.ThrowsAsync<ActiveBookingsLimitExceededException>(() =>
            bookingService.CreateBookingAsync(ev.Id, userId));
    }

    [Fact]
    public async Task CreateBookingAsync_LimitIsPerUser()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var ev = await eventService.CreateAsync(BuildRequest(DateTime.UtcNow.AddDays(30)));
        var firstUser = Guid.NewGuid();

        for (var i = 0; i < ActiveBookingsLimit; i++)
            await bookingService.CreateBookingAsync(ev.Id, firstUser);

        var booking = await bookingService.CreateBookingAsync(ev.Id, Guid.NewGuid());

        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_RejectedBookingDoesNotCountTowardLimit()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var ev = await eventService.CreateAsync(BuildRequest(DateTime.UtcNow.AddDays(30)));
        var userId = Guid.NewGuid();

        var first = await bookingService.CreateBookingAsync(ev.Id, userId);
        for (var i = 1; i < ActiveBookingsLimit; i++)
            await bookingService.CreateBookingAsync(ev.Id, userId);

        await bookingService.UpdateBookingStatusAsync(
            first.Id, BookingStatus.Rejected, DateTime.UtcNow, CancellationToken.None);

        var extra = await bookingService.CreateBookingAsync(ev.Id, userId);

        Assert.Equal(BookingStatus.Pending, extra.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_ConfirmedBookingCountsTowardLimit()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var ev = await eventService.CreateAsync(BuildRequest(DateTime.UtcNow.AddDays(30)));
        var userId = Guid.NewGuid();

        var first = await bookingService.CreateBookingAsync(ev.Id, userId);
        for (var i = 1; i < ActiveBookingsLimit; i++)
            await bookingService.CreateBookingAsync(ev.Id, userId);

        await bookingService.UpdateBookingStatusAsync(
            first.Id, BookingStatus.Confirmed, DateTime.UtcNow, CancellationToken.None);

        await Assert.ThrowsAsync<ActiveBookingsLimitExceededException>(() =>
            bookingService.CreateBookingAsync(ev.Id, userId));
    }
}
