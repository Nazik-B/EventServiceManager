using BookingsService.Infrastructure.DataAccess;
using BookingsService.Infrastructure.Repositories;
using BookingsService.Application.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookingsService.IntegrationTests;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgresTestContainer _fixture;

    protected ServiceProvider Services { get; }
    protected BookingsDbContext DbContext { get; }

    protected IntegrationTestBase(PostgresTestContainer fixture)
    {
        _fixture = fixture;

        var services = new ServiceCollection();

        services.AddDbContext<BookingsDbContext>(options =>
            options.UseNpgsql(fixture.ConnectionString));

        services.AddScoped<IBookingRepository, BookingRepository>();

        Services = services.BuildServiceProvider();
        DbContext = Services.GetRequiredService<BookingsDbContext>();
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
