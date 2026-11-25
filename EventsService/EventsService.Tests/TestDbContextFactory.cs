using EventsService.Application.Abstractions;
using EventsService.Application.Caching;
using EventsService.Application.Repositories;
using EventsService.Application.Services;
using EventsService.Infrastructure.DataAccess;
using EventsService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EventServiceImplementation = EventsService.Application.Services.EventService;

namespace EventsService.Tests;

public static class TestDbContextFactory
{
    public static ServiceProvider CreateServiceProvider()
    {
        var dbName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();

        services.AddDbContext<EventsDbContext>(options =>
            options.UseInMemoryDatabase(dbName));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventServiceImplementation>();

        services.AddSingleton<InMemoryCacheService>();
        services.AddSingleton<ICacheService>(sp => sp.GetRequiredService<InMemoryCacheService>());
        services.AddOptions<CacheOptions>();

        return services.BuildServiceProvider();
    }
}
