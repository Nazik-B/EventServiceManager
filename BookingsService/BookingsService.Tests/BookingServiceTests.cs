using BookingsService.Application.Services;
using BookingsService.Domain.Entities;
using BookingsService.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingsService.Tests;

public class BookingServiceTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private readonly ServiceProvider _sp = TestServices.Create();

    private static IBookingService Svc(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IBookingService>();

    [Fact]
    public async Task CreateBookingAsync_ReturnsPendingBooking()
    {
        using var scope = _sp.CreateScope();
        var eventId = Guid.NewGuid();

        var booking = await Svc(scope).CreateBookingAsync(eventId, TestUserId);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Null(booking.ProcessedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_AssignsUniqueIds()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var eventId = Guid.NewGuid();

        var first = await svc.CreateBookingAsync(eventId, TestUserId);
        var second = await svc.CreateBookingAsync(eventId, TestUserId);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task CreateBookingAsync_Throws_WhenEventIdIsEmpty()
    {
        using var scope = _sp.CreateScope();

        await Assert.ThrowsAsync<DomainValidationException>(
            () => Svc(scope).CreateBookingAsync(Guid.Empty, TestUserId));
    }

    [Fact]
    public async Task CreateBookingAsync_Throws_WhenUserIdIsEmpty()
    {
        using var scope = _sp.CreateScope();

        await Assert.ThrowsAsync<DomainValidationException>(
            () => Svc(scope).CreateBookingAsync(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReturnsBooking_ForOwner()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var created = await svc.CreateBookingAsync(Guid.NewGuid(), TestUserId);

        var found = await svc.GetBookingByIdAsync(created.Id, TestUserId, isAdmin: false);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Equal(created.EventId, found.EventId);
        Assert.Equal(BookingStatus.Pending, found.Status);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReturnsNull_ForUnknownId()
    {
        using var scope = _sp.CreateScope();

        var found = await Svc(scope).GetBookingByIdAsync(Guid.NewGuid(), TestUserId, isAdmin: false);

        Assert.Null(found);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ThrowsForbidden_WhenUserIsNotOwner()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var created = await svc.CreateBookingAsync(Guid.NewGuid(), TestUserId);

        await Assert.ThrowsAsync<ForbiddenOperationException>(
            () => svc.GetBookingByIdAsync(created.Id, Guid.NewGuid(), isAdmin: false));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ReturnsAnyBooking_ForAdmin()
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var created = await svc.CreateBookingAsync(Guid.NewGuid(), TestUserId);

        var found = await svc.GetBookingByIdAsync(created.Id, Guid.NewGuid(), isAdmin: true);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    public async Task UpdateBookingStatusAsync_ChangesStatusAndProcessedAt(BookingStatus finalStatus)
    {
        using var scope = _sp.CreateScope();
        var svc = Svc(scope);
        var created = await svc.CreateBookingAsync(Guid.NewGuid(), TestUserId);
        var processedAt = DateTime.UtcNow;

        await svc.UpdateBookingStatusAsync(created.Id, finalStatus, processedAt, CancellationToken.None);

        var updated = await svc.GetBookingByIdAsync(created.Id, TestUserId, isAdmin: false);
        Assert.NotNull(updated);
        Assert.Equal(finalStatus, updated!.Status);
        Assert.Equal(processedAt, updated.ProcessedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_AssignsUniqueIds_WhenRequestsAreConcurrent()
    {
        const int concurrentRequests = 10;

        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _sp.CreateScope();
                return await Svc(scope).CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid());
            }))
            .ToArray();

        var bookings = await Task.WhenAll(tasks);

        Assert.Equal(concurrentRequests, bookings.Select(b => b.Id).Distinct().Count());
    }

    [Fact]
    public async Task CreateBookingAsync_DoesNotExceedLimit_WhenRequestsAreConcurrent()
    {
        const int limit = 10;
        const int concurrentRequests = 20;
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _sp.CreateScope();
                try
                {
                    await Svc(scope).CreateBookingAsync(eventId, userId);
                    return (Exception?)null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(limit, results.Count(r => r is null));
        Assert.All(
            results.Where(r => r is not null),
            r => Assert.IsType<ActiveBookingsLimitExceededException>(r));
    }
}
