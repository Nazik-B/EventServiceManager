using System.Text.Json;
using BookingsService.Application.Abstractions;
using BookingsService.Domain.Entities;
using BookingsService.Infrastructure.DataAccess;
using EventServiceManager.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookingsService.Infrastructure.BackgroundServices;

public sealed class OutboxPublisherBackgroundService : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinFailureDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxFailureDelay = TimeSpan.FromSeconds(60);
    private const int SummaryLogEveryNthFailure = 10;

    private int _consecutiveFailures;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = IdleDelay;

            try
            {
                var (sent, failed) = await PublishBatchAsync(stoppingToken);

                if (failed)
                {
                    delay = GetBackoffDelay();
                }
                else
                {
                    if (_consecutiveFailures > 0)
                    {
                        _logger.LogInformation(
                            "Outbox publishing recovered after {Failures} consecutive failures.",
                            _consecutiveFailures);
                        _consecutiveFailures = 0;
                    }

                    if (sent == BatchSize) continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogError(ex, "Outbox publishing cycle failed.");
                delay = GetBackoffDelay();
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("OutboxPublisherBackgroundService stopped.");
    }

    private TimeSpan GetBackoffDelay()
    {
        var exponent = Math.Min(Math.Max(_consecutiveFailures - 1, 0), 6);
        var seconds = MinFailureDelay.TotalSeconds * Math.Pow(2, exponent);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxFailureDelay.TotalSeconds));
    }

    private async Task<(int Sent, bool Failed)> PublishBatchAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IBookingEventPublisher>();

        await using var transaction = await db.Database.BeginTransactionAsync(stoppingToken);

        var messages = await db.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAt" IS NULL
                ORDER BY "OccurredAt"
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(stoppingToken);

        var sent = 0;
        var failed = false;

        foreach (var message in messages)
        {
            try
            {
                await PublishAsync(publisher, message, stoppingToken);
                message.MarkProcessed(DateTime.UtcNow);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _consecutiveFailures++;
                message.MarkFailed(ex.Message);

                if (_consecutiveFailures == 1)
                {
                    _logger.LogWarning(ex,
                        "Failed to publish outbox message {MessageId} (attempt {Attempts}). Retrying with backoff.",
                        message.Id, message.Attempts);
                }
                else if (_consecutiveFailures % SummaryLogEveryNthFailure == 0)
                {
                    _logger.LogWarning(
                        "Outbox message {MessageId} is still not published: {Error} (consecutive failures: {Failures}, attempt {Attempts}).",
                        message.Id, ex.Message, _consecutiveFailures, message.Attempts);
                }
                failed = true;
                break;
            }
        }

        // Фиксируем уже отправленное даже при остановке сервиса, чтобы не плодить дубликаты.
        await db.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        return (sent, failed);
    }

    private static Task PublishAsync(IBookingEventPublisher publisher, OutboxMessage message, CancellationToken ct)
    {
        if (message.Type != nameof(BookingConfirmed))
            throw new InvalidOperationException($"Unknown outbox message type '{message.Type}'.");

        var payload = JsonSerializer.Deserialize<BookingConfirmed>(message.Payload)
            ?? throw new InvalidOperationException("Outbox payload is empty.");

        return publisher.PublishBookingConfirmedAsync(payload, ct);
    }
}