using EventsApi.Models;

namespace EventsApi.Services;

public class BookingService : IBookingService
{
    private readonly List<Booking> _bookings = new();

    public IEnumerable<Booking> GetAll() => _bookings;

    public Booking? GetById(Guid id) =>
        _bookings.FirstOrDefault(b => b.Id == id);

    public Booking Create(Guid eventId)
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
        return booking;
    }

    public bool Delete(Guid id)
    {
        var existing = GetById(id);
        if (existing is null)
            return false;

        _bookings.Remove(existing);
        return true;
    }
}