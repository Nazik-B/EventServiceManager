using Microsoft.Extensions.DependencyInjection;
using BookingsService.Application.Services;

namespace BookingsService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IBookingProcessingService, BookingProcessingService>();
        return services;
    }
}
