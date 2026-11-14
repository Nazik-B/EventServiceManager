using EventsApi.Domain.Entities;
using EventsApi.Application.Repositories;
using EventsApi.Domain.Exceptions;
using EventsApi.Application.Dto;

namespace EventsApi.Application.Services;

public class BookingService : IBookingService
{
    private const int MaxActiveBookingsPerUser = 10;

    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    private readonly IBookingRepository _bookingRepository;
    private readonly IEventRepository _eventRepository;

    public BookingService(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository)
    {
        _bookingRepository = bookingRepository;
        _eventRepository = eventRepository;
    }

    public async Task<BookingResponse> CreateBookingAsync(Guid eventId, Guid userId)
    {
        await BookingSemaphore.WaitAsync();

        try
        {
            var eventItem = await _eventRepository
                .GetByIdTrackedAsync(eventId);

            if (eventItem is null)
            {
                throw new NotFoundException(
                    $"Event with id {eventId} was not found");
            }

            if (eventItem.StartAt <= DateTime.UtcNow)
            {
                throw new PastEventBookingException();
            }

            var activeCount = await _bookingRepository
                .CountActiveByUserAsync(userId);

            if (activeCount >= MaxActiveBookingsPerUser)
            {
                throw new ActiveBookingsLimitExceededException(MaxActiveBookingsPerUser);
            }

            var reserved = eventItem.TryReserveSeats();

            if (!reserved)
            {
                throw new NoAvailableSeatsException();
            }

            var booking = Booking.Create(eventId, userId);

            await _bookingRepository.AddAsync(booking);
            await _bookingRepository.SaveChangesAsync();

            return ToResponse(booking);
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<BookingResponse?> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        return booking is null ? null : ToResponse(booking);
    }

    public async Task<IEnumerable<Booking>> GetPendingBookingsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _bookingRepository.GetPendingAsync(cancellationToken);
    }

    public async Task UpdateBookingStatusAsync(
        Guid bookingId,
        BookingStatus status,
        DateTime processedAt,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdTrackedAsync(
            bookingId,
            cancellationToken);

        if (booking is null)
        {
            return;
        }

        booking.Status = status;
        booking.ProcessedAt = processedAt;

        await _bookingRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<BookingResponse> CancelBookingAsync(
        Guid bookingId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await BookingSemaphore.WaitAsync(cancellationToken);

        try
        {
            var booking = await _bookingRepository.GetByIdTrackedAsync(
                bookingId,
                cancellationToken);

            if (booking is null)
            {
                throw new NotFoundException(
                    $"Booking with id {bookingId} was not found");
            }

            if (booking.UserId != userId && !isAdmin)
            {
                throw new ForbiddenOperationException(
                    "You can cancel only your own bookings");
            }

            booking.Cancel();

            var eventItem = await _eventRepository.GetByIdTrackedAsync(
                booking.EventId,
                cancellationToken);

            eventItem?.ReleaseSeats();

            await _bookingRepository.SaveChangesAsync(cancellationToken);

            return ToResponse(booking);
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }
    private static BookingResponse ToResponse(Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        Status = booking.Status,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };
}