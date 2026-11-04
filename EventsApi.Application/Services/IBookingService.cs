using EventsApi.Domain.Entities;
using EventsApi.Application.Dto;

namespace EventsApi.Application.Services;

public interface IBookingService
{
    Task<BookingResponse> CreateBookingAsync(Guid eventId);
    Task<BookingResponse?> GetBookingByIdAsync(Guid bookingId);
    Task<IEnumerable<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
    Task UpdateBookingStatusAsync(Guid bookingId, BookingStatus status, DateTime processedAt, CancellationToken cancellationToken = default);
}