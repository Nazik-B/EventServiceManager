using EventsApi.DataAccess;
using EventsApi.Repositories;
using EventsApi.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventApi.IntegrationTests;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgresTestContainer _fixture;

    protected ServiceProvider Services { get; }
    protected AppDbContext DbContext { get; }

    protected IntegrationTestBase(PostgresTestContainer fixture)
    {
        _fixture = fixture;

        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(fixture.ConnectionString));

        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IEventRepository, EventRepository>();

        Services = services.BuildServiceProvider();
        DbContext = Services.GetRequiredService<AppDbContext>();
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