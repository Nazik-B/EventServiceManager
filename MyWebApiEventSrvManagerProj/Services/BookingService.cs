using EventsApi.Models;
using MyWebApiEventSrvManagerProj.Exceptions;

namespace EventsApi.Services;

public class BookingService : IBookingService
{
    private readonly IEventService _eventService;
    private readonly List<Booking> _bookings = new();
    private readonly object _bookingLock = new(); 
    private readonly object _lock = new();

    public BookingService(IEventService eventService)
    {
        _eventService = eventService;
    }

    public Task<Booking> CreateBookingAsync(Guid eventId)
    {
        lock (_bookingLock)
        {
            var eventItem = _eventService.GetById(eventId);
            if (eventItem is null)
            {
                throw new NotFoundException($"Event with id {eventId} was not found");
            }

            var reserved = _eventService.TryReserveSeat(eventId);

            if (!reserved)
            {
                throw new NoAvailableSeatsException();
            }

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = null
            };

           _bookings.Add(booking);

            return Task.FromResult(booking);
        }
    }

    public Task<Booking?> GetBookingByIdAsync(Guid bookingId)
    {
        lock (_lock)
        {
            var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
            return Task.FromResult(booking);
        }
    }

    public Task<IEnumerable<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            var pending = _bookings.Where(b => b.Status == BookingStatus.Pending).ToList();
            return Task.FromResult<IEnumerable<Booking>>(pending);
        }
    }

    public Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status, DateTime processedAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        lock (_lock)
        {
            var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
            if (booking is not null)
            {
                booking.Status = status;
                booking.ProcessedAt = processedAt;
            }
        }

        return Task.CompletedTask;
    }
}