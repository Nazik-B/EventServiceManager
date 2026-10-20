using EventsApi.Application.Repositories;
using EventsApi.Domain.Entities;

namespace EventsApi.Application.Services;

public class BookingProcessingService : IBookingProcessingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventRepository _eventRepository;

    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository)
    {
        _bookingRepository = bookingRepository;
        _eventRepository = eventRepository;
    }

    public async Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _bookingRepository.GetPendingIdsAsync(cancellationToken);
    }

    public async Task<BookingProcessingResult> ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdForUpdateAsync(bookingId, cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Pending)
        {
            return BookingProcessingResult.Skipped;
        }

        var eventItem = await _eventRepository.GetByIdForUpdateAsync(booking.EventId, cancellationToken);

        booking.ProcessedAt = DateTime.UtcNow;

        if (eventItem is null)
        {
            booking.Status = BookingStatus.Rejected;
            await _bookingRepository.SaveChangesAsync(cancellationToken);
            return BookingProcessingResult.RejectedEventNotFound;
        }

        booking.Status = BookingStatus.Confirmed;
        await _bookingRepository.SaveChangesAsync(cancellationToken);
        return BookingProcessingResult.Confirmed;
    }

    public async Task<bool> RejectAndReleaseSeatAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdForUpdateAsync(bookingId, cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Pending)
        {
            return false;
        }

        var eventItem = await _eventRepository.GetByIdForUpdateAsync(booking.EventId, cancellationToken);

        booking.Status = BookingStatus.Rejected;
        booking.ProcessedAt = DateTime.UtcNow;

        eventItem?.ReleaseSeats();

        await _bookingRepository.SaveChangesAsync(cancellationToken);
        return true;
    }
}