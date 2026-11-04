namespace EventsApi.Application.Services;

public interface IBookingProcessingService
{
    Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default);

    Task<BookingProcessingResult> ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<bool> RejectAndReleaseSeatAsync(Guid bookingId, CancellationToken cancellationToken = default);
}