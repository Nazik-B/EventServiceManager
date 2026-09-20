using EventsApi.DataAccess;
using EventsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MyWebApiEventSrvManagerProj.BackgroundServices;

public class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    public BookingProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BookingProcessingBackgroundService started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pendingBookingIds = await GetPendingBookingIdsAsync(
                    stoppingToken);

                var tasks = pendingBookingIds.Select(
                    bookingId => ProcessBookingAsync(
                        bookingId,
                        stoppingToken));

                await Task.WhenAll(tasks);

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Нормальная остановка приложения.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error while processing pending bookings.");
        }
        finally
        {
            _logger.LogInformation(
                "BookingProcessingBackgroundService stopped.");
        }
    }

    private async Task<List<Guid>> GetPendingBookingIdsAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        return await context.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.Pending)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessBookingAsync(
        Guid bookingId,
        CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation(
                "Processing booking {BookingId}.",
                bookingId);

            await Task.Delay(ProcessingDelay, stoppingToken);

            await using var scope = _scopeFactory.CreateAsyncScope();

            var context = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var booking = await context.Bookings
                .FirstOrDefaultAsync(
                    item => item.Id == bookingId,
                    stoppingToken);

            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                return;
            }

            var eventItem = await context.Events
                .FirstOrDefaultAsync(
                    item => item.Id == booking.EventId,
                    stoppingToken);

            var processedAt = DateTime.UtcNow;

            if (eventItem is null)
            {
                booking.Status = BookingStatus.Rejected;
                booking.ProcessedAt = processedAt;

                await context.SaveChangesAsync(stoppingToken);

                _logger.LogWarning(
                    "Booking {BookingId} was rejected because event {EventId} was not found.",
                    booking.Id,
                    booking.EventId);

                return;
            }

            booking.Status = BookingStatus.Confirmed;
            booking.ProcessedAt = processedAt;

            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "Booking {BookingId} confirmed at {ProcessedAt}.",
                booking.Id,
                processedAt);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Processing of booking {BookingId} was cancelled.",
                bookingId);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while processing booking {BookingId}.",
                bookingId);

            await RejectBookingAndReleaseSeatAsync(bookingId);
        }
    }

    private async Task RejectBookingAndReleaseSeatAsync(Guid bookingId)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var context = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var booking = await context.Bookings
                .FirstOrDefaultAsync(item => item.Id == bookingId);

            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                return;
            }

            var eventItem = await context.Events
                .FirstOrDefaultAsync(
                    item => item.Id == booking.EventId);

            booking.Status = BookingStatus.Rejected;
            booking.ProcessedAt = DateTime.UtcNow;

            if (eventItem is not null)
            {
                eventItem.ReleaseSeats();
            }

            await context.SaveChangesAsync();

            _logger.LogWarning(
                "Booking {BookingId} was rejected and a seat was released.",
                booking.Id);
        }
        catch (Exception rejectionEx)
        {
            _logger.LogError(
                rejectionEx,
                "Failed to reject booking {BookingId}.",
                bookingId);
        }
    }
}