using BookingsService.Domain.Entities;
using BookingsService.Domain.Exceptions;
using Xunit;

namespace BookingsService.Tests;

public class BookingCreateValidationTests
{
    [Fact]
    public void Create_Throws_WhenEventIdIsEmpty()
    {
        Assert.Throws<DomainValidationException>(
            () => Booking.Create(Guid.Empty, Guid.NewGuid()));
    }
}
