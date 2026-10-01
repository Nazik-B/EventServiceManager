using EventsApi.Models;
using EventsApi.Repositories.Interfaces;

namespace MyWebApiEventSrvManagerProj.BackgroundServices;

public sealed class BookingProcessingBackgroundService : BackgroundService
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

    private async Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var bookingRepository = scope.ServiceProvider
            .GetRequiredService<IBookingRepository>();

        return await bookingRepository.GetPendingIdsAsync(
            cancellationToken);
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

            var bookingRepository = scope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

            var eventRepository = scope.ServiceProvider
                .GetRequiredService<IEventRepository>();

            var booking = await bookingRepository.GetByIdForUpdateAsync(
                bookingId,
                stoppingToken);

            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                return;
            }

            var eventItem = await eventRepository.GetByIdForUpdateAsync(
                booking.EventId,
                stoppingToken);

            var processedAt = DateTime.UtcNow;

            if (eventItem is null)
            {
                booking.Status = BookingStatus.Rejected;
                booking.ProcessedAt = processedAt;

                await bookingRepository.SaveChangesAsync(stoppingToken);

                _logger.LogWarning(
                    "Booking {BookingId} was rejected because event {EventId} was not found.",
                    booking.Id,
                    booking.EventId);

                return;
            }

            booking.Status = BookingStatus.Confirmed;
            booking.ProcessedAt = processedAt;

            await bookingRepository.SaveChangesAsync(stoppingToken);

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

            var bookingRepository = scope.ServiceProvider
                .GetRequiredService<IBookingRepository>();

            var eventRepository = scope.ServiceProvider
                .GetRequiredService<IEventRepository>();

            var booking = await bookingRepository.GetByIdForUpdateAsync(
                bookingId);

            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                return;
            }

            var eventItem = await eventRepository.GetByIdForUpdateAsync(
                booking.EventId);

            booking.Status = BookingStatus.Rejected;
            booking.ProcessedAt = DateTime.UtcNow;

            eventItem?.ReleaseSeats();

            await bookingRepository.SaveChangesAsync();

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