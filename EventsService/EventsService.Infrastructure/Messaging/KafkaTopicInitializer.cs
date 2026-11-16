using Confluent.Kafka;
using Confluent.Kafka.Admin;
using EventServiceManager.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventsService.Infrastructure.Messaging;

public sealed class KafkaTopicInitializer : IHostedService
{
    private const int Partitions = 3;
    private const short ReplicationFactor = 1;

    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaTopicInitializer> _logger;

    public KafkaTopicInitializer(IOptions<KafkaOptions> options, ILogger<KafkaTopicInitializer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var admin = new AdminClientBuilder(
                new AdminClientConfig { BootstrapServers = _options.BootstrapServers }).Build();

            await admin.CreateTopicsAsync(
                new[]
                {
                    new TopicSpecification
                    {
                        Name = KafkaTopics.BookingConfirmed,
                        NumPartitions = Partitions,
                        ReplicationFactor = ReplicationFactor
                    }
                },
                new CreateTopicsOptions
                {
                    RequestTimeout = TimeSpan.FromSeconds(10),
                    OperationTimeout = TimeSpan.FromSeconds(10)
                });

            _logger.LogInformation("Kafka topic {Topic} created.", KafkaTopics.BookingConfirmed);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            _logger.LogInformation("Kafka topic {Topic} already exists.", KafkaTopics.BookingConfirmed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not ensure Kafka topic {Topic}; continuing startup.", KafkaTopics.BookingConfirmed);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
