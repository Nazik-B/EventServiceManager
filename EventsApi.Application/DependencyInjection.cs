using EventsApi.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventsApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();
        return services;
    }
}