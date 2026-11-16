using EventServiceManager.Contracts;

namespace EventsService.Application.Services;

public interface IBookingConfirmedHandler
{
    Task<BookingConfirmedResult> HandleAsync(BookingConfirmed message, CancellationToken cancellationToken = default);
}
