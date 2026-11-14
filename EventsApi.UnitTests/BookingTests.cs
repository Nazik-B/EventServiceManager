using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;
using Xunit;

namespace EventsApi.UnitTests;

public class BookingTests
{
    private static Booking CreateBooking() => Booking.Create(Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Create_SetsPendingStatusAndUser()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var booking = Booking.Create(eventId, userId);

        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(userId, booking.UserId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public void Create_Throws_WhenUserIdIsEmpty()
    {
        Assert.Throws<DomainValidationException>(
            () => Booking.Create(Guid.NewGuid(), Guid.Empty));
    }

    [Theory]
    [InlineData(BookingStatus.Pending)]
    [InlineData(BookingStatus.Confirmed)]
    public void Cancel_SetsCancelledStatus(BookingStatus initialStatus)
    {
        var booking = CreateBooking();
        booking.Status = initialStatus;

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void Cancel_Throws_WhenAlreadyCancelled()
    {
        var booking = CreateBooking();
        booking.Cancel();

        Assert.Throws<DomainValidationException>(() => booking.Cancel());
        Assert.Equal(BookingStatus.Cancelled, booking.Status);
    }

    [Fact]
    public void Cancel_Throws_WhenRejected()
    {
        var booking = CreateBooking();
        booking.Status = BookingStatus.Rejected;

        Assert.Throws<DomainValidationException>(() => booking.Cancel());
        Assert.Equal(BookingStatus.Rejected, booking.Status);
    }
}
