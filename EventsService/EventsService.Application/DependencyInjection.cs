using Microsoft.Extensions.DependencyInjection;
using EventsService.Application.Services;

namespace EventsService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();
        return services;
    }
}
