using EventsService.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace EventsService.Infrastructure;

public static class KafkaMessagingDependencyInjection
{
    // Порядок важен: сначала создаётся топик, потом стартует подписчик.
    public static IServiceCollection AddKafkaMessaging(this IServiceCollection services)
    {
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<BookingConfirmedConsumer>();
        return services;
    }
}
