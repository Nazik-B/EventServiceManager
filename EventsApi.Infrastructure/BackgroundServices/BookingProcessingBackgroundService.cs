using EventsApi.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventsApi.Infrastructure.BackgroundServices;

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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BookingProcessingBackgroundService started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pendingBookingIds = await GetPendingBookingIdsAsync(stoppingToken);

                var tasks = pendingBookingIds.Select(
                    bookingId => ProcessBookingAsync(bookingId, stoppingToken));

                await Task.WhenAll(tasks);

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
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

    private async Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var processingService = scope.ServiceProvider
            .GetRequiredService<IBookingProcessingService>();

        return await processingService.GetPendingBookingIdsAsync(cancellationToken);
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Processing booking {BookingId}.", bookingId);

            await Task.Delay(ProcessingDelay, stoppingToken);

            await using var scope = _scopeFactory.CreateAsyncScope();

            var processingService = scope.ServiceProvider
                .GetRequiredService<IBookingProcessingService>();

            var result = await processingService.ProcessBookingAsync(bookingId, stoppingToken);

            switch (result)
            {
                case BookingProcessingResult.Confirmed:
                    _logger.LogInformation("Booking {BookingId} confirmed.", bookingId);
                    break;
                case BookingProcessingResult.RejectedEventNotFound:
                    _logger.LogWarning(
                        "Booking {BookingId} was rejected because its event was not found.",
                        bookingId);
                    break;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Processing of booking {BookingId} was cancelled.", bookingId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing booking {BookingId}.", bookingId);

            await RejectBookingAndReleaseSeatAsync(bookingId);
        }
    }

    private async Task RejectBookingAndReleaseSeatAsync(Guid bookingId)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();

            var processingService = scope.ServiceProvider
                .GetRequiredService<IBookingProcessingService>();

            var rejected = await processingService.RejectAndReleaseSeatAsync(bookingId);

            if (rejected)
            {
                _logger.LogWarning(
                    "Booking {BookingId} was rejected and a seat was released.",
                    bookingId);
            }
        }
        catch (Exception rejectionEx)
        {
            _logger.LogError(rejectionEx, "Failed to reject booking {BookingId}.", bookingId);
        }
    }
}