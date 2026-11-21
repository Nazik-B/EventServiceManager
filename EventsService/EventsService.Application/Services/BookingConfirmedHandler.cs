using EventServiceManager.Contracts;
using EventsService.Application.Repositories;

namespace EventsService.Application.Services;

public sealed class BookingConfirmedHandler : IBookingConfirmedHandler
{
    private readonly IEventRepository _eventRepository;

    public BookingConfirmedHandler(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<BookingConfirmedResult> HandleAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default)
    {
        if (message.SeatsCount <= 0)
        {
            return BookingConfirmedResult.InvalidMessage;
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

        await _eventRepository.SaveChangesAsync();

        return BookingConfirmedResult.SeatsReserved;
    }
}

