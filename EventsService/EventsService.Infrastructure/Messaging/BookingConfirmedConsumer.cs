using System.Text.Json;
using Confluent.Kafka;
using EventServiceManager.Contracts;
using EventsService.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventsService.Infrastructure.Messaging;

public sealed class BookingConfirmedConsumer : BackgroundService
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<BookingConfirmedConsumer> _logger;

    public BookingConfirmedConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<BookingConfirmedConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    // Consume блокирует поток, поэтому уходим с потока запуска хоста.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        try
        {
            using var consumer = new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, error) => _logger.LogWarning(
                    "Kafka consumer error: {Reason} (code {Code}).", error.Reason, error.Code))
                .Build();

            consumer.Subscribe(KafkaTopics.BookingConfirmed);

            _logger.LogInformation(
                "Subscribed to {Topic} as consumer group {Group}.",
                KafkaTopics.BookingConfirmed, _options.ConsumerGroup);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    ConsumeResult<string, string>? result;

                    try
                    {
                        result = consumer.Consume(stoppingToken);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogWarning(ex, "Kafka consume error.");
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                        continue;
                    }

                    if (result?.Message is null)
                    {
                        continue;
                    }

                    await HandleWithRetryAsync(result, stoppingToken);

                    consumer.Commit(result);
                }
            }
            finally
            {
                consumer.Close();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BookingConfirmed consumer stopped because of an unexpected error.");
        }
        finally
        {
            _logger.LogInformation("BookingConfirmedConsumer stopped.");
        }
    }

    private async Task HandleWithRetryAsync(ConsumeResult<string, string> result, CancellationToken stoppingToken)
    {
        var message = Deserialize(result);

        if (message is null)
        {
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var handler = scope.ServiceProvider.GetRequiredService<IBookingConfirmedHandler>();
                var outcome = await handler.HandleAsync(message, stoppingToken);

                LogOutcome(message, outcome);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxAttempts} failed for booking {BookingId}; retrying.",
                    attempt, MaxAttempts, message.BookingId);

                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Giving up on booking {BookingId} after {MaxAttempts} attempts; message skipped.",
                    message.BookingId, MaxAttempts);
                return;
            }
        }
    }

    private BookingConfirmed? Deserialize(ConsumeResult<string, string> result)
    {
        try
        {
            var message = string.IsNullOrWhiteSpace(result.Message.Value)
                ? null
                : JsonSerializer.Deserialize<BookingConfirmed>(result.Message.Value);

            if (message is null)
            {
                _logger.LogError("Empty message at {Position} skipped.", result.TopicPartitionOffset);
            }

            return message;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Malformed message at {Position} skipped.", result.TopicPartitionOffset);
            return null;
        }
    }

    private void LogOutcome(BookingConfirmed message, BookingConfirmedResult outcome)
    {
        switch (outcome)
        {
            case BookingConfirmedResult.SeatsReserved:
                _logger.LogInformation(
                    "Reserved {Seats} seat(s) of event {EventId} for booking {BookingId}.",
                    message.SeatsCount, message.EventId, message.BookingId);
                break;
            case BookingConfirmedResult.EventNotFound:
                _logger.LogWarning(
                    "Event {EventId} not found; booking {BookingId} skipped.",
                    message.EventId, message.BookingId);
                break;
            case BookingConfirmedResult.NoAvailableSeats:
                _logger.LogWarning(
                    "No available seats in event {EventId}; booking {BookingId} skipped.",
                    message.EventId, message.BookingId);
                break;
            case BookingConfirmedResult.InvalidMessage:
                _logger.LogWarning(
                    "Invalid seats count {Seats} in booking {BookingId}; message skipped.",
                    message.SeatsCount, message.BookingId);
                break;
            case BookingConfirmedResult.Duplicate:
                _logger.LogWarning(
                    "Duplicate BookingConfirmed for booking {BookingId}; already processed, skipped.",
                    message.BookingId);
                break;
        }
    }
}
