using BookingsService.Application.Abstractions;
using EventServiceManager.Contracts;

namespace BookingsService.IntegrationTests;

internal sealed class NoOpBookingEventPublisher : IBookingEventPublisher
{
    public Task PublishBookingConfirmedAsync(BookingConfirmed message, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
