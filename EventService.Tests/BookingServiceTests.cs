using EventsApi.Models;
using EventsApi.Models.Dto;
using EventsApi.Services;
using MyWebApiEventSrvManagerProj.Exceptions;
using Xunit;

namespace EventService.Tests;

public class BookingServiceTests
{
    private static (BookingService bookingService, EventsApi.Services.EventService eventService) CreateServices()
    {
        var eventService = new EventsApi.Services.EventService();
        var bookingService = new BookingService(eventService);
        return (bookingService, eventService);
    }

    private static CreateEventRequest BuildEventRequest(string title = "Team Meeting") =>
        new()
        {
            Title = title,
            StartAt = DateTime.Parse("2026-08-01T10:00:00"),
            EndAt = DateTime.Parse("2026-08-01T11:00:00")
        };

    // Успешные сценарии

    [Fact]
    public async Task CreateBookingAsync_ReturnsBookingWithPendingStatus_ForExistingEvent()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest());

        var booking = await bookingService.CreateBookingAsync(createdEvent.Id);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(createdEvent.Id, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Null(booking.ProcessedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_AssignsUniqueIds_ForMultipleBookingsOfSameEvent()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest());

        var first = await bookingService.CreateBookingAsync(createdEvent.Id);
        var second = await bookingService.CreateBookingAsync(createdEvent.Id);
        var third = await bookingService.CreateBookingAsync(createdEvent.Id);

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(Guid.Empty, second.Id);
        Assert.NotEqual(Guid.Empty, third.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(second.Id, third.Id);
        Assert.NotEqual(first.Id, third.Id);
        Assert.All(new[] { first, second, third }, b => Assert.Equal(createdEvent.Id, b.EventId));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReturnsCorrectBooking_WhenExists()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest());
        var created = await bookingService.CreateBookingAsync(createdEvent.Id);

        var found = await bookingService.GetBookingByIdAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal(created.EventId, found.EventId);
        Assert.Equal(BookingStatus.Pending, found.Status);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    public async Task GetBookingByIdAsync_ReflectsStatusChange_AfterProcessing(BookingStatus finalStatus)
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest());
        var created = await bookingService.CreateBookingAsync(createdEvent.Id);
        var processedAt = DateTime.UtcNow;

        await bookingService.UpdateBookingStatusAsync(created.Id, finalStatus, processedAt, CancellationToken.None);
        var updated = await bookingService.GetBookingByIdAsync(created.Id);

        Assert.NotNull(updated);
        Assert.Equal(finalStatus, updated!.Status);
        Assert.Equal(processedAt, updated.ProcessedAt);
    }

    // Неуспешные сценарии

    [Fact]
    public async Task CreateBookingAsync_ThrowsNotFoundException_ForNonExistentEvent()
    {
        var (bookingService, _) = CreateServices();
        var nonExistentEventId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(
            () => bookingService.CreateBookingAsync(nonExistentEventId));
    }

    [Fact]
    public async Task CreateBookingAsync_ThrowsNotFoundException_ForDeletedEvent()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest());
        eventService.Delete(createdEvent.Id);

        await Assert.ThrowsAsync<NotFoundException>(
            () => bookingService.CreateBookingAsync(createdEvent.Id));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReturnsNull_ForNonExistentId()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest());
        await bookingService.CreateBookingAsync(createdEvent.Id);

        var found = await bookingService.GetBookingByIdAsync(Guid.NewGuid());

        Assert.Null(found);
    }
}