using BookingsService.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BookingsService.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresTestContainer _db = new();

    public async Task InitializeAsync() => await _db.InitializeAsync();

    async Task IAsyncLifetime.DisposeAsync() => await _db.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.ConnectionString);
        builder.UseSetting("Jwt:Secret", TestTokens.Secret);
        builder.UseSetting("Jwt:Issuer", TestTokens.Issuer);
        builder.UseSetting("Jwt:Audience", TestTokens.Audience);
        builder.UseSetting("Jwt:LifetimeMinutes", "60");
        builder.UseSetting("Kafka:BootstrapServers", "localhost:9092");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBookingEventPublisher>();
            services.AddSingleton<IBookingEventPublisher, NoOpBookingEventPublisher>();
        });
    }
}
