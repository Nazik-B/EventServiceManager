using EventsApi.Models;

namespace EventsApi.Services;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId);
    Task<Booking?> GetBookingByIdAsync(Guid bookingId);
}