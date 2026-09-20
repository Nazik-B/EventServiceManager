using EventsApi.DataAccess;
using EventsApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EventServiceImplementation = EventsApi.Services.EventService;

namespace EventService.Tests;

public static class TestDbContextFactory
{
    public static ServiceProvider CreateServiceProvider()
    {
        var dbName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.AddScoped<IEventService, EventServiceImplementation>();
        services.AddScoped<IBookingService, BookingService>();

        return services.BuildServiceProvider();
    }
}