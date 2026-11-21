namespace EventsService.Application;

public interface IProcessedBookingRepository
{
    Task<bool> ExistsAsync(Guid bookingId, CancellationToken ct);
    void Add(Guid bookingId);
}

public sealed class DuplicateBookingException : Exception { }