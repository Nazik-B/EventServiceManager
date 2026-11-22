using System.Text.Json;
using BookingsService.Application.Repositories;
using BookingsService.Domain.Entities;
using EventServiceManager.Contracts;

namespace BookingsService.Application.Services;

public class BookingProcessingService : IBookingProcessingService
{
    private const int SeatsPerBooking = 1;

    private readonly IBookingRepository _bookingRepository;
    private readonly IOutboxRepository _outboxRepository;

    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IOutboxRepository outboxRepository)
    {
        _bookingRepository = bookingRepository;
        _outboxRepository = outboxRepository;
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

        // Статус брони и сообщение outbox сохраняются одной транзакцией.
        // В Kafka сообщение отправляет OutboxPublisherBackgroundService.
        var message = new BookingConfirmed(
            booking.Id,
            booking.EventId,
            booking.UserId,
            SeatsPerBooking,
            confirmedAt);

        await _outboxRepository.AddAsync(
            OutboxMessage.Create(
                nameof(BookingConfirmed),
                KafkaTopics.BookingConfirmed,
                booking.EventId.ToString(),
                JsonSerializer.Serialize(message),
                confirmedAt),
            cancellationToken);

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
