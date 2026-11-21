using EventServiceManager.Contracts;
using EventsService.Application.Repositories;

namespace EventsService.Application.Services;

public sealed class BookingConfirmedHandler : IBookingConfirmedHandler
{
    private readonly IEventRepository _eventRepository;
    private readonly IProcessedBookingRepository _processedBookings;

    public BookingConfirmedHandler(
        IEventRepository eventRepository,
        IProcessedBookingRepository processedBookings)
    {
        _eventRepository = eventRepository;
        _processedBookings = processedBookings;
    }

    public async Task<BookingConfirmedResult> HandleAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default)
    {
        if (message.SeatsCount <= 0)
        {
            return BookingConfirmedResult.InvalidMessage;
        }

        if (await _processedBookings.ExistsAsync(message.BookingId, cancellationToken))
        {
            return BookingConfirmedResult.Duplicate;
        }

        var eventItem = await _eventRepository.GetByIdTrackedAsync(message.EventId, cancellationToken);
        if (eventItem is null)
        {
            return BookingConfirmedResult.EventNotFound;
        }

        if (!eventItem.TryReserveSeats(message.SeatsCount))
        {
            return BookingConfirmedResult.NoAvailableSeats;
        }

        _processedBookings.Add(message.BookingId);

        try
        {
            await _eventRepository.SaveChangesAsync();
        }
        catch (DuplicateBookingException)
        {
            return BookingConfirmedResult.Duplicate;
        }

        return BookingConfirmedResult.SeatsReserved;
    }
}