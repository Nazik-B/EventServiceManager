using EventsApi.Models;
using EventsApi.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MyWebApiEventSrvManagerProj.BackgroundServices;

public class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    public BookingProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BookingProcessingBackgroundService started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var bookingService = scope.ServiceProvider
                    .GetRequiredService<IBookingService>();

                var pendingBookings = await bookingService
                    .GetPendingBookingsAsync(stoppingToken);

                foreach (var booking in pendingBookings)
                {
                    await ProcessBookingAsync(booking, stoppingToken);
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Штатная остановка приложения.
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

    private async Task ProcessBookingAsync(
        Booking booking,
        CancellationToken stoppingToken)
    {
        await _processingSemaphore.WaitAsync(stoppingToken);

        try
        {
            _logger.LogInformation(
                "Processing booking {BookingId} for event {EventId}.",
                booking.Id,
                booking.EventId);

            await Task.Delay(ProcessingDelay, stoppingToken);

            await using var scope = _scopeFactory.CreateAsyncScope();

            var bookingService = scope.ServiceProvider
                .GetRequiredService<IBookingService>();

            var eventService = scope.ServiceProvider
                .GetRequiredService<IEventService>();

            var processedAt = DateTime.UtcNow;

            var eventItem = await eventService
                .GetByIdAsync(booking.EventId);

            if (eventItem is null)
            {
                await bookingService.UpdateBookingStatusAsync(
                    booking.Id,
                    BookingStatus.Rejected,
                    processedAt,
                    stoppingToken);

                _logger.LogWarning(
                    "Booking {BookingId} was rejected because event {EventId} was not found.",
                    booking.Id,
                    booking.EventId);

                return;
            }

            await bookingService.UpdateBookingStatusAsync(
                booking.Id,
                BookingStatus.Confirmed,
                processedAt,
                stoppingToken);

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
                booking.Id);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while processing booking {BookingId}.",
                booking.Id);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var bookingService = scope.ServiceProvider
                    .GetRequiredService<IBookingService>();

                var eventService = scope.ServiceProvider
                    .GetRequiredService<IEventService>();

                var processedAt = DateTime.UtcNow;

                await bookingService.UpdateBookingStatusAsync(
                    booking.Id,
                    BookingStatus.Rejected,
                    processedAt,
                    CancellationToken.None);

                await eventService.ReleaseSeatAsync(booking.EventId);

                _logger.LogWarning(
                    "Booking {BookingId} was rejected and a seat was released.",
                    booking.Id);
            }
            catch (Exception rejectionEx)
            {
                _logger.LogError(
                    rejectionEx,
                    "Failed to reject booking {BookingId}.",
                    booking.Id);
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }
}