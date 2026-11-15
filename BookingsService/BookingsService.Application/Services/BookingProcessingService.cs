using BookingsService.Application.Abstractions;
using BookingsService.Application.Repositories;
using BookingsService.Domain.Entities;
using EventServiceManager.Contracts;

namespace BookingsService.Application.Services;

public class BookingProcessingService : IBookingProcessingService
{
    private const int SeatsPerBooking = 1;

    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingEventPublisher _eventPublisher;

    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IBookingEventPublisher eventPublisher)
    {
        _bookingRepository = bookingRepository;
        _eventPublisher = eventPublisher;
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

        var confirmedAt = DateTime.UtcNow;

        booking.Status = BookingStatus.Confirmed;
        booking.ProcessedAt = confirmedAt;

        // 1. Сначала сохраняем статус в своей базе.
        await _bookingRepository.SaveChangesAsync(cancellationToken);

        // 2. Только потом публикуем событие.
        await _eventPublisher.PublishBookingConfirmedAsync(
            new BookingConfirmed(
                booking.Id,
                booking.EventId,
                booking.UserId,
                SeatsPerBooking,
                confirmedAt),
            cancellationToken);

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
