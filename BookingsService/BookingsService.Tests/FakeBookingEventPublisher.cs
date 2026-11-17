using BookingsService.Application.Abstractions;
using EventServiceManager.Contracts;

namespace BookingsService.Tests;

internal sealed class FakeBookingEventPublisher : IBookingEventPublisher
{
    private readonly List<BookingConfirmed> _published = new();

    public Exception? ExceptionToThrow { get; set; }

    public IReadOnlyList<BookingConfirmed> Published
    {
        get { lock (_published) return _published.ToList(); }
    }

    public Task PublishBookingConfirmedAsync(BookingConfirmed message, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;

        lock (_published) _published.Add(message);
        return Task.CompletedTask;
    }
}
