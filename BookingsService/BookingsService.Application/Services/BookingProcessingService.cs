using BookingsService.Application.Repositories;
using BookingsService.Domain.Entities;

namespace BookingsService.Application.Services;

public class BookingProcessingService : IBookingProcessingService
{
    private readonly IBookingRepository _bookingRepository;

    public BookingProcessingService(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    public async Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _bookingRepository.GetPendingIdsAsync(cancellationToken);
    }

    public async Task<BookingProcessingResult> ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdTrackedAsync(bookingId, cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Pending)
        {
            return BookingProcessingResult.Skipped;
        }

        booking.Status = BookingStatus.Confirmed;
        booking.ProcessedAt = DateTime.UtcNow;

        await _bookingRepository.SaveChangesAsync(cancellationToken);

        return BookingProcessingResult.Confirmed;
    }

    public async Task<bool> RejectAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdTrackedAsync(bookingId, cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Pending)
        {
            return false;
        }

        booking.Status = BookingStatus.Rejected;
        booking.ProcessedAt = DateTime.UtcNow;

        await _bookingRepository.SaveChangesAsync(cancellationToken);

        return true;
    }
}
