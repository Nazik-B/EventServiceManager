using EventsApi.DataAccess;
using EventsApi.Models;
using System.ComponentModel.DataAnnotations;
using EventsApi.Models.Dto;
using EventsApi.Services;
using MyWebApiEventSrvManagerProj.Exceptions;
using Xunit;

namespace EventService.Tests;

public class BookingServiceTests
{
    private static (
        AppDbContext context,
        BookingService bookingService,
        EventsApi.Services.EventService eventService) CreateServices()
    {
        var context = TestDbContextFactory.Create();

        var eventService = new EventsApi.Services.EventService(context);
        var bookingService = new BookingService(context);

        return (context, bookingService, eventService);
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
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

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
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

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
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

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
        var (context, bookingService, _) = CreateServices();
        await using var _ = context;

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
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

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
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

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
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

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
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest(totalSeats: 1));

        await bookingService.CreateBookingAsync(createdEvent.Id);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => bookingService.CreateBookingAsync(createdEvent.Id));
    }

    [Fact]
    public async Task CreateBookingAsync_ThrowsNotFoundException_ForNonExistentEvent()
    {
        var (context, bookingService, _) = CreateServices();
        await using var _ = context;

        await Assert.ThrowsAsync<NotFoundException>(
            () => bookingService.CreateBookingAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateBookingAsync_ThrowsNotFoundException_ForDeletedEvent()
    {
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

        var createdEvent = await eventService.CreateAsync(
            BuildEventRequest());

        await eventService.DeleteAsync(createdEvent.Id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => bookingService.CreateBookingAsync(createdEvent.Id));
    }

    [Fact]
    public async Task RejectAndReleaseSeat_RestoresAvailableSeats()
    {
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

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
        var (context, bookingService, eventService) = CreateServices();
        await using var _ = context;

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
}