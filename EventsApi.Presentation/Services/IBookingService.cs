using EventsApi.Models;

namespace EventsApi.Services;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId);
    Task<Booking?> GetBookingByIdAsync(Guid bookingId);
    Task<IEnumerable<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
    Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status, DateTime processedAt, CancellationToken cancellationToken = default);
}