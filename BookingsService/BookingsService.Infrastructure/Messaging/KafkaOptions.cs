using System.ComponentModel.DataAnnotations;

namespace BookingsService.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    [Required]
    public string BootstrapServers { get; set; } = string.Empty;
}
