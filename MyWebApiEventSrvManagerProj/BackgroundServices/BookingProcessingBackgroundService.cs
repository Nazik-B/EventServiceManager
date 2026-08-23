using EventsApi.Models;
using EventsApi.Services;

namespace MyWebApiEventSrvManagerProj.BackgroundServices;

public class BookingProcessingBackgroundService : BackgroundService
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    public BookingProcessingBackgroundService(
        IBookingService bookingService,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingProcessingBackgroundService started.");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pendingBookings = await _bookingService.GetPendingBookingsAsync(stoppingToken);

                var tasks = pendingBookings
                .Select(booking => ProcessBookingAsync(booking, stoppingToken));

                await Task.WhenAll(tasks);

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Штатное завершение сервиса при остановке приложения.            
        }            
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing pending bookings.");
        }
        finally
        {
            _logger.LogInformation("BookingProcessingBackgroundService stopped.");
        }
    }

    private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing booking {BookingId} for event {EventId}.", booking.Id, booking.EventId);

        await Task.Delay(ProcessingDelay, stoppingToken);

        stoppingToken.ThrowIfCancellationRequested();

        var processedAt = DateTime.UtcNow;
        
        await _bookingService.UpdateBookingStatusAsync(booking.Id, BookingStatus.Confirmed, processedAt, stoppingToken);

        _logger.LogInformation(
            "Booking {BookingId} confirmed at {ProcessedAt}.", booking.Id, processedAt);
    }
}