using EventsApi.Models;

namespace EventsApi.Services;

public class BookingService : IBookingService
{
    private readonly List<Booking> _bookings = new();

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
}