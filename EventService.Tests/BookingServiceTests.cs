using EventsApi.DataAccess;
using EventsApi.Models;
using System.ComponentModel.DataAnnotations;
using EventsApi.Models.Dto;
using EventsApi.Services;
using MyWebApiEventSrvManagerProj.Exceptions;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace EventService.Tests;

public class BookingServiceTests
{
    private readonly ServiceProvider _serviceProvider;
    
    public BookingServiceTests()
    {
        _serviceProvider = TestDbContextFactory.CreateServiceProvider();
    }

    private static CreateEventRequest BuildEventRequest(
        string title = "Team Meeting",
        int totalSeats = 3)
    {
        return new CreateEventRequest
        {
            Title = title,
            StartAt = DateTime.Parse("2026-08-01T10:00:00"),
            EndAt = DateTime.Parse("2026-08-01T11:00:00"),
            TotalSeats = totalSeats
        };
    }

    [Fact]
    public async Task CreateBookingAsync_ReturnsPendingBooking_ForExistingEvent()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest());

        var booking = await bookingService.CreateBookingAsync(
            createdEvent.Id);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(createdEvent.Id, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Null(booking.ProcessedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_AssignsUniqueIds_ForMultipleBookings()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest(totalSeats: 3));

        var first = await bookingService.CreateBookingAsync(createdEvent.Id);
        var second = await bookingService.CreateBookingAsync(createdEvent.Id);
        var third = await bookingService.CreateBookingAsync(createdEvent.Id);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(second.Id, third.Id);
        Assert.NotEqual(first.Id, third.Id);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReturnsCorrectBooking_WhenExists()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest());

        var created = await bookingService.CreateBookingAsync(
            createdEvent.Id);

        var found = await bookingService.GetBookingByIdAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal(created.EventId, found.EventId);
        Assert.Equal(BookingStatus.Pending, found.Status);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReturnsNull_ForNonExistentId()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var found = await bookingService.GetBookingByIdAsync(
            Guid.NewGuid());

        Assert.Null(found);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    public async Task UpdateBookingStatusAsync_ChangesStatusAndProcessedAt(
        BookingStatus finalStatus)
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest());

        var created = await bookingService.CreateBookingAsync(
            createdEvent.Id);

        var processedAt = DateTime.UtcNow;

        await bookingService.UpdateBookingStatusAsync(
            created.Id,
            finalStatus,
            processedAt,
            CancellationToken.None);

        var updated = await bookingService.GetBookingByIdAsync(created.Id);

        Assert.NotNull(updated);
        Assert.Equal(finalStatus, updated!.Status);
        Assert.Equal(processedAt, updated.ProcessedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_DecreasesAvailableSeatsByOne()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest(totalSeats: 3));

        await bookingService.CreateBookingAsync(createdEvent.Id);

        var updatedEvent = await eventService.GetByIdAsync(createdEvent.Id);

        Assert.NotNull(updatedEvent);
        Assert.Equal(2, updatedEvent!.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_UsesAllSeatsWithoutOverbooking()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest(totalSeats: 3));

        await bookingService.CreateBookingAsync(createdEvent.Id);
        await bookingService.CreateBookingAsync(createdEvent.Id);
        await bookingService.CreateBookingAsync(createdEvent.Id);

        var updatedEvent = await eventService.GetByIdAsync(createdEvent.Id);

        Assert.NotNull(updatedEvent);
        Assert.Equal(0, updatedEvent!.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_ThrowsNoAvailableSeatsException_WhenSeatsExhausted()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest(totalSeats: 1));

        await bookingService.CreateBookingAsync(createdEvent.Id);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => bookingService.CreateBookingAsync(createdEvent.Id));
    }

    [Fact]
    public async Task CreateBookingAsync_ThrowsNotFoundException_ForNonExistentEvent()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        await Assert.ThrowsAsync<NotFoundException>(
            () => bookingService.CreateBookingAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateBookingAsync_ThrowsNotFoundException_ForDeletedEvent()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest());

        await eventService.DeleteAsync(createdEvent.Id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => bookingService.CreateBookingAsync(createdEvent.Id));
    }

    [Fact]
    public async Task RejectAndReleaseSeat_RestoresAvailableSeats()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest(totalSeats: 1));

        var booking = await bookingService.CreateBookingAsync(
            createdEvent.Id);

        await bookingService.UpdateBookingStatusAsync(
            booking.Id,
            BookingStatus.Rejected,
            DateTime.UtcNow,
            CancellationToken.None);

        var released = await eventService.ReleaseSeatAsync(
            createdEvent.Id);

        var updatedEvent = await eventService.GetByIdAsync(
            createdEvent.Id);

        Assert.True(released);
        Assert.NotNull(updatedEvent);
        Assert.Equal(1, updatedEvent!.AvailableSeats);
    }

    [Fact]
    public async Task RejectAndReleaseSeat_AllowsNewBooking()
    {
        using var scope = _serviceProvider.CreateScope();

        var bookingService = scope.ServiceProvider
            .GetRequiredService<IBookingService>();

        var eventService = scope.ServiceProvider
            .GetRequiredService<IEventService>();

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest(totalSeats: 1));

        var rejectedBooking = await bookingService.CreateBookingAsync(
            createdEvent.Id);

        await bookingService.UpdateBookingStatusAsync(
            rejectedBooking.Id,
            BookingStatus.Rejected,
            DateTime.UtcNow,
            CancellationToken.None);

        await eventService.ReleaseSeatAsync(createdEvent.Id);

        var newBooking = await bookingService.CreateBookingAsync(
            createdEvent.Id);

        var updatedEvent = await eventService.GetByIdAsync(
            createdEvent.Id);

        Assert.NotEqual(rejectedBooking.Id, newBooking.Id);
        Assert.Equal(BookingStatus.Pending, newBooking.Status);
        Assert.NotNull(updatedEvent);
        Assert.Equal(0, updatedEvent!.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_DoesNotAllowOverbooking_WhenRequestsAreConcurrent()
    {
        const int totalSeats = 5;
        const int concurrentRequests = 20;

        Guid eventId;

        using (var setupScope = _serviceProvider.CreateScope())
        {
            var setupEventService = setupScope.ServiceProvider
                .GetRequiredService<IEventService>();

            var createdEvent = await setupEventService.CreateAsync(
                BuildEventRequest(totalSeats: totalSeats));

            eventId = createdEvent.Id;
        }

        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(async () =>
            {
                using var requestScope = _serviceProvider.CreateScope();

                var bookingService = requestScope.ServiceProvider
                    .GetRequiredService<IBookingService>();

                try
                {
                    var booking = await bookingService
                        .CreateBookingAsync(eventId);

                    return (Booking: booking, Exception: (Exception?)null);
                }
                catch (Exception ex)
                {
                    return (Booking: (Booking?)null, Exception: ex);
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var successfulBookings = results
            .Where(result => result.Booking is not null)
            .Select(result => result.Booking!)
            .ToList();

        var exceptions = results
            .Where(result => result.Exception is not null)
            .Select(result => result.Exception!)
            .ToList();

        Assert.Equal(totalSeats, successfulBookings.Count);

        Assert.Equal(
            concurrentRequests - totalSeats,
            exceptions.Count);

        Assert.All(
            exceptions,
            exception => Assert.IsType<NoAvailableSeatsException>(
                exception));

        using var verificationScope = _serviceProvider.CreateScope();

        var verificationEventService = verificationScope.ServiceProvider
            .GetRequiredService<IEventService>();

        var updatedEvent = await verificationEventService
            .GetByIdAsync(eventId);

        Assert.NotNull(updatedEvent);
        Assert.Equal(0, updatedEvent!.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_AssignsUniqueIds_WhenRequestsAreConcurrent()
    {
        const int totalSeats = 10;
        const int concurrentRequests = 10;

        Guid eventId;

        using (var setupScope = _serviceProvider.CreateScope())
        {
            var setupEventService = setupScope.ServiceProvider
                .GetRequiredService<IEventService>();

            var createdEvent = await setupEventService.CreateAsync(
                BuildEventRequest(totalSeats: totalSeats));

            eventId = createdEvent.Id;
        }

        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(async () =>
            {
                using var requestScope = _serviceProvider.CreateScope();

                var bookingService = requestScope.ServiceProvider
                    .GetRequiredService<IBookingService>();

                return await bookingService.CreateBookingAsync(eventId);
            }))
            .ToArray();

        var bookings = await Task.WhenAll(tasks);

        var uniqueBookingIds = bookings
            .Select(booking => booking.Id)
            .Distinct()
            .Count();

        Assert.Equal(concurrentRequests, bookings.Length);
        Assert.Equal(concurrentRequests, uniqueBookingIds);

        using var verificationScope = _serviceProvider.CreateScope();

        var verificationEventService = verificationScope.ServiceProvider
            .GetRequiredService<IEventService>();

        var updatedEvent = await verificationEventService
            .GetByIdAsync(eventId);

        Assert.NotNull(updatedEvent);
        Assert.Equal(0, updatedEvent!.AvailableSeats);
    }
}