using System.ComponentModel.DataAnnotations;

namespace EventsService.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required]
    public string BootstrapServers { get; set; } = string.Empty;

    [Required]
    public string ConsumerGroup { get; set; } = string.Empty;
}
