using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EventsApi.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgresTestContainer _db = new();

    public async Task InitializeAsync() => await _db.InitializeAsync();

    async Task IAsyncLifetime.DisposeAsync() => await _db.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.ConnectionString);
        builder.UseSetting("Jwt:Secret", "integration-tests-secret-0123456789abcdef");
        builder.UseSetting("Jwt:Issuer", "EventsApi");
        builder.UseSetting("Jwt:Audience", "EventsApi.Clients");
        builder.UseSetting("Jwt:LifetimeMinutes", "60");
    }
}