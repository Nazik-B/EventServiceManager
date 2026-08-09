using EventsApi.Models;

namespace EventsApi.Services;

public class BookingService : IBookingService
{
    private readonly List<Booking> _bookings = new();
    private readonly object _lock = new();

    public Task<Booking> CreateBookingAsync(Guid eventId)
    {
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

    public Task<Booking?> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = _bookings.FirstOrDefault(b => b.Id == bookingId);
        return Task.FromResult(booking);
    }

     public Task<IEnumerable<Booking>> GetPendingBookingsAsync()
    {
        lock (_lock)
        {
            var pending = _bookings.Where(b => b.Status == BookingStatus.Pending).ToList();
            return Task.FromResult<IEnumerable<Booking>>(pending);
        }
    }

    public Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status, DateTime processedAt)
    {
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