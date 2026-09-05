using EventsApi.Models;
using EventsApi.Services;

namespace MyWebApiEventSrvManagerProj.BackgroundServices;

public class BookingProcessingBackgroundService : BackgroundService
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;
    private readonly IEventService _eventService;
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    public BookingProcessingBackgroundService(
        IBookingService bookingService,
        IEventService eventService,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingService = bookingService;
        _eventService = eventService;
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

        var semaphoreEntered = false;
        try
        {
            await Task.Delay(ProcessingDelay, stoppingToken);

            stoppingToken.ThrowIfCancellationRequested();

            await _processingSemaphore.WaitAsync(stoppingToken);

            semaphoreEntered = true;
        
            var processedAt = DateTime.UtcNow;
            var eventItem = _eventService.GetById(booking.EventId);

            if (eventItem is null)
            {
                await _bookingService.UpdateBookingStatusAsync(booking.Id, BookingStatus.Rejected, processedAt, stoppingToken);

                _logger.LogWarning(
                    "Booking {BookingId} was rejected because event {EventId} was not found.",
                    booking.Id,
                    booking.EventId);

                return;
            }

            await _bookingService.UpdateBookingStatusAsync(booking.Id, BookingStatus.Confirmed, processedAt, stoppingToken);

            _logger.LogInformation(
            "Booking {BookingId} confirmed at {ProcessedAt}.", booking.Id, processedAt);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
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

                if (!semaphoreEntered)
                {
                    await _processingSemaphore.WaitAsync(CancellationToken.None);
                    semaphoreEntered = true;
                }

            var processedAt = DateTime.UtcNow;

            await _bookingService.UpdateBookingStatusAsync(booking.Id, BookingStatus.Rejected, processedAt, CancellationToken.None);

            _eventService.ReleaseSeat(booking.EventId);

            _logger.LogWarning(
                "Booking {BookingId} was rejected and a seat was released.",
                booking.Id);
        }
        finally
        {
            if (semaphoreEntered)
            {
                _processingSemaphore.Release();
            }  
        }
    }
}