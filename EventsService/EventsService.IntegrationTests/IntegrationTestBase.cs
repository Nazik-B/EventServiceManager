using EventsService.Infrastructure.DataAccess;
using EventsService.Infrastructure.Repositories;
using EventsService.Application.Abstractions;
using EventsService.Application.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EventsService.Application;
using EventsService.Application.Services;
using EventsService.Infrastructure;

namespace EventsService.IntegrationTests;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgresTestContainer _fixture;

    protected ServiceProvider Services { get; }
    protected EventsDbContext DbContext { get; }

    protected IntegrationTestBase(PostgresTestContainer fixture)
    {
        _fixture = fixture;

        var services = new ServiceCollection();

        services.AddDbContext<EventsDbContext>(options =>
            options.UseNpgsql(fixture.ConnectionString));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IProcessedBookingRepository, ProcessedBookingRepository>();
        services.AddScoped<IBookingConfirmedHandler, BookingConfirmedHandler>();

        services.AddSingleton<InMemoryCacheService>();
        services.AddSingleton<ICacheService>(sp => sp.GetRequiredService<InMemoryCacheService>());

        Services = services.BuildServiceProvider();
        DbContext = Services.GetRequiredService<EventsDbContext>();
    }

    public async Task InitializeAsync()
    {
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await Services.DisposeAsync();
    }
}
