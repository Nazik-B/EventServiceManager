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

    private static CreateEventRequest BuildEventRequest(string title = "Team Meeting", int totalSeats = 3) =>
        new()
        {
            Title = title,
            StartAt = DateTime.Parse("2026-08-01T10:00:00"),
            EndAt = DateTime.Parse("2026-08-01T11:00:00"),
            TotalSeats = totalSeats
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

       // Новая логика мест

    [Fact]
    public async Task CreateBookingAsync_DecreasesAvailableSeatsByOne()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest(totalSeats: 3));

        await bookingService.CreateBookingAsync(createdEvent.Id);

        var updatedEvent = eventService.GetById(createdEvent.Id);

        Assert.NotNull(updatedEvent);
        Assert.Equal(2, updatedEvent.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_CreatesBookingsWithUniqueIds_UntilSeatLimit()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest(totalSeats: 3));

        var first = await bookingService.CreateBookingAsync(createdEvent.Id);
        var second = await bookingService.CreateBookingAsync(createdEvent.Id);
        var third = await bookingService.CreateBookingAsync(createdEvent.Id);

        var bookingIds = new[] { first.Id, second.Id, third.Id };
        var updatedEvent = eventService.GetById(createdEvent.Id);

        Assert.Equal(3, bookingIds.Distinct().Count());
        Assert.NotNull(updatedEvent);
        Assert.Equal(0, updatedEvent.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_ThrowsNoAvailableSeatsException_WhenSeatsAreExhausted()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest(totalSeats: 1));

        await bookingService.CreateBookingAsync(createdEvent.Id);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => bookingService.CreateBookingAsync(createdEvent.Id));
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
    // Смена статуса брони

    [Fact]
    public async Task UpdateBookingStatusAsync_SetsConfirmedStatusAndProcessedAt()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest());
        var booking = await bookingService.CreateBookingAsync(createdEvent.Id);
        var processedAt = DateTime.UtcNow;

        await bookingService.UpdateBookingStatusAsync(
            booking.Id,
            BookingStatus.Confirmed,
            processedAt,
            CancellationToken.None);

        var updatedBooking = await bookingService.GetBookingByIdAsync(booking.Id);

        Assert.NotNull(updatedBooking);
        Assert.Equal(BookingStatus.Confirmed, updatedBooking!.Status);
        Assert.Equal(processedAt, updatedBooking.ProcessedAt);
    }

    [Fact]
    public async Task UpdateBookingStatusAsync_SetsRejectedStatusAndProcessedAt()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest());
        var booking = await bookingService.CreateBookingAsync(createdEvent.Id);
        var processedAt = DateTime.UtcNow;

        await bookingService.UpdateBookingStatusAsync(
            booking.Id,
            BookingStatus.Rejected,
            processedAt,
            CancellationToken.None);

        var updatedBooking = await bookingService.GetBookingByIdAsync(booking.Id);

        Assert.NotNull(updatedBooking);
        Assert.Equal(BookingStatus.Rejected, updatedBooking!.Status);
        Assert.Equal(processedAt, updatedBooking.ProcessedAt);
    }

    [Fact]
    public async Task RejectAndReleaseSeat_RestoresAvailableSeats()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest(totalSeats: 1));
        var booking = await bookingService.CreateBookingAsync(createdEvent.Id);

        await bookingService.UpdateBookingStatusAsync(
            booking.Id,
            BookingStatus.Rejected,
            DateTime.UtcNow,
            CancellationToken.None);

        var released = eventService.ReleaseSeat(createdEvent.Id);
        var updatedEvent = eventService.GetById(createdEvent.Id);

        Assert.True(released);
        Assert.NotNull(updatedEvent);
        Assert.Equal(1, updatedEvent.AvailableSeats);
    }

    [Fact]
    public async Task RejectAndReleaseSeat_AllowsCreatingNewBooking()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest(totalSeats: 1));
        var rejectedBooking = await bookingService.CreateBookingAsync(createdEvent.Id);

        await bookingService.UpdateBookingStatusAsync(
            rejectedBooking.Id,
            BookingStatus.Rejected,
            DateTime.UtcNow,
            CancellationToken.None);

        eventService.ReleaseSeat(createdEvent.Id);

        var newBooking = await bookingService.CreateBookingAsync(createdEvent.Id);
        var updatedEvent = eventService.GetById(createdEvent.Id);

        Assert.NotEqual(rejectedBooking.Id, newBooking.Id);
        Assert.Equal(BookingStatus.Pending, newBooking.Status);
        Assert.NotNull(updatedEvent);
        Assert.Equal(0, updatedEvent.AvailableSeats);
    }

    // Конкурентность

    [Fact]
    public async Task CreateBookingAsync_DoesNotAllowOverbooking_WhenRequestsAreConcurrent()
    {
        var (bookingService, eventService) = CreateServices();
        var createdEvent = eventService.Create(BuildEventRequest(totalSeats: 5));

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    var booking = await bookingService.CreateBookingAsync(createdEvent.Id);
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

        var updatedEvent = eventService.GetById(createdEvent.Id);

        Assert.Equal(5, successfulBookings.Count);
        Assert.Equal(15, exceptions.Count);
        Assert.All(exceptions, exception =>
            Assert.IsType<NoAvailableSeatsException>(exception));

        Assert.NotNull(updatedEvent);
        Assert.Equal(0, updatedEvent.AvailableSeats);
    }
}