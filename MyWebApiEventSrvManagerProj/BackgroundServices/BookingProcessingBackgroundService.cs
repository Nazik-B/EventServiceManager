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

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pendingBookings = await _bookingService.GetPendingBookingsAsync();

                foreach (var booking in pendingBookings)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    await ProcessBookingAsync(booking, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing pending bookings.");
            }

            await Task.Delay(PollingInterval, stoppingToken).ContinueWith(_ => { });
        }

        _logger.LogInformation("BookingProcessingBackgroundService stopped.");
    }

    private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing booking {BookingId} for event {EventId}.", booking.Id, booking.EventId);

        await Task.Delay(ProcessingDelay, stoppingToken).ContinueWith(_ => { });

        var processedAt = DateTime.UtcNow;
        await _bookingService.UpdateBookingStatusAsync(booking.Id, BookingStatus.Confirmed, processedAt);

        _logger.LogInformation(
            "Booking {BookingId} confirmed at {ProcessedAt}.", booking.Id, processedAt);
    }
}