using EventServiceManager.Contracts;

namespace BookingsService.Application.Abstractions;

public interface IBookingEventPublisher
{
    Task PublishBookingConfirmedAsync(BookingConfirmed message, CancellationToken cancellationToken = default);
}
