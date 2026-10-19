using EventsApi.Models;
using EventsApi.Repositories.Interfaces;
using MyWebApiEventSrvManagerProj.Exceptions;

namespace EventsApi.Services;

public class BookingService : IBookingService
{
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

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        await BookingSemaphore.WaitAsync();

        try
        {
            var eventItem = await _eventRepository
                .GetByIdForUpdateAsync(eventId);

            if (eventItem is null)
            {
                throw new NotFoundException(
                    $"Event with id {eventId} was not found");
            }

            var reserved = eventItem.TryReserveSeats();

            if (!reserved)
            {
                throw new NoAvailableSeatsException();
            }

            var booking = Booking.Create(eventId);

            await _bookingRepository.AddAsync(booking);
            await _bookingRepository.SaveChangesAsync();

            return booking;
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public Task<Booking?> GetBookingByIdAsync(Guid bookingId)
    {
        return _bookingRepository.GetByIdAsync(bookingId);
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
        var booking = await _bookingRepository.GetByIdForUpdateAsync(
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
}