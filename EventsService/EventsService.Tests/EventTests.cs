using EventsService.Domain.Exceptions;
using EventsService.Domain.Entities;
using Xunit;

namespace EventsService.Tests;

public class EventTests
{
    private static Event Build(int totalSeats = 3) => Event.Create(
        Guid.NewGuid(),
        "Domain Event",
        null,
        DateTime.UtcNow.AddDays(1),
        DateTime.UtcNow.AddDays(1).AddHours(1),
        totalSeats);

    [Fact]
    public void Create_SetsAvailableSeatsEqualToTotal()
    {
        var ev = Build(totalSeats: 5);

        Assert.Equal(5, ev.TotalSeats);
        Assert.Equal(5, ev.AvailableSeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_Throws_ForNonPositiveTotalSeats(int totalSeats)
    {
        Assert.Throws<DomainValidationException>(() => Build(totalSeats));
    }

    [Fact]
    public void TryReserveSeats_DecreasesAvailableSeats()
    {
        var ev = Build(totalSeats: 3);

        var reserved = ev.TryReserveSeats();

        Assert.True(reserved);
        Assert.Equal(2, ev.AvailableSeats);
    }

    [Fact]
    public void TryReserveSeats_ReturnsFalse_AndKeepsSeats_WhenNotEnough()
    {
        var ev = Build(totalSeats: 1);
        ev.TryReserveSeats();

        var reserved = ev.TryReserveSeats();

        Assert.False(reserved);
        Assert.Equal(0, ev.AvailableSeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TryReserveSeats_Throws_ForNonPositiveCount(int count)
    {
        var ev = Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => ev.TryReserveSeats(count));
    }

    [Fact]
    public void ReleaseSeats_RestoresSeat()
    {
        var ev = Build(totalSeats: 2);
        ev.TryReserveSeats();

        ev.ReleaseSeats();

        Assert.Equal(2, ev.AvailableSeats);
    }

    [Fact]
    public void ReleaseSeats_Throws_WhenExceedsTotalCapacity()
    {
        var ev = Build(totalSeats: 2);

        Assert.Throws<DomainValidationException>(() => ev.ReleaseSeats());
        Assert.Equal(2, ev.AvailableSeats);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReleaseSeats_Throws_ForNonPositiveCount(int count)
    {
        var ev = Build();

        Assert.Throws<ArgumentOutOfRangeException>(() => ev.ReleaseSeats(count));
    }
}

