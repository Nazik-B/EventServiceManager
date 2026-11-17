using System.Text.Json;
using BookingsService.Application.Abstractions;
using Confluent.Kafka;
using EventServiceManager.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookingsService.Infrastructure.Messaging;

public sealed class KafkaBookingEventPublisher : IBookingEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaBookingEventPublisher> _logger;

    public KafkaBookingEventPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaBookingEventPublisher> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 10_000
        };

        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, error) => _logger.LogWarning(
                "Kafka producer error: {Reason} (code {Code}).", error.Reason, error.Code))
            .Build();
    }

    public async Task PublishBookingConfirmedAsync(
        BookingConfirmed message,
        CancellationToken cancellationToken = default)
    {
        var kafkaMessage = new Message<string, string>
        {
            Key = message.EventId.ToString(),
            Value = JsonSerializer.Serialize(message)
        };

        try
        {
            var result = await _producer.ProduceAsync(
                KafkaTopics.BookingConfirmed, kafkaMessage, cancellationToken);

            _logger.LogInformation(
                "BookingConfirmed for booking {BookingId} published to {Topic} [partition {Partition}, offset {Offset}].",
                message.BookingId, result.Topic, result.Partition.Value, result.Offset.Value);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex,
                "Failed to publish BookingConfirmed for booking {BookingId}.", message.BookingId);
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _producer.Dispose();
        }
    }
}
