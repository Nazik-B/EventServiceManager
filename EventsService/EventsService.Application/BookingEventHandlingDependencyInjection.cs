using EventsService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventsService.Application;

public static class BookingEventHandlingDependencyInjection
{
    public static IServiceCollection AddBookingEventHandling(this IServiceCollection services)
    {
        services.AddScoped<IBookingConfirmedHandler, BookingConfirmedHandler>();
        return services;
    }
}
