using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace EventsService.Infrastructure.Caching;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddRedisConnection(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'Redis' is not configured.");

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;

        // ConnectionMultiplexer is thread-safe and expensive to create: one instance for the whole app.
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        return services;
    }
}