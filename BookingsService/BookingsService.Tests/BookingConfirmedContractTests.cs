using System.Text.Json;
using EventServiceManager.Contracts;
using Xunit;

namespace BookingsService.Tests;

public class BookingConfirmedContractTests
{
    [Fact]
    public void TopicName_IsBookingConfirmed()
    {
        Assert.Equal("booking-confirmed", KafkaTopics.BookingConfirmed);
    }

    [Fact]
    public void Json_RoundTrip_PreservesAllFields()
    {
        var original = new BookingConfirmed(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            SeatsCount: 1,
            ConfirmedAt: new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc));

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<BookingConfirmed>(json);

        Assert.Equal(original, restored);
    }
}
