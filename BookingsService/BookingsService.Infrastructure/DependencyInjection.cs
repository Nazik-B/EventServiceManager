using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BookingsService.Application.Abstractions;
using BookingsService.Application.Repositories;
using BookingsService.Infrastructure.BackgroundServices;
using BookingsService.Infrastructure.DataAccess;
using BookingsService.Infrastructure.Messaging;
using BookingsService.Infrastructure.Repositories;

namespace BookingsService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<BookingsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddSingleton<IBookingEventPublisher, KafkaBookingEventPublisher>();
        services.AddHostedService<BookingProcessingBackgroundService>();
        services.AddHostedService<OutboxPublisherBackgroundService>();
        return services;
    }

    public static void ApplyMigrations(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Database.Migrate();
    }
}
