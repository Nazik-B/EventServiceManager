using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EventsService.Application.Repositories;
using EventsService.Infrastructure.DataAccess;
using EventsService.Infrastructure.Repositories;
using EventsService.Application;

namespace EventsService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string? connectionString)
    {
        services.AddDbContext<EventsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IProcessedBookingRepository, ProcessedBookingRepository>();
        return services;
    }

    public static void ApplyMigrations(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database.Migrate();
    }
}
