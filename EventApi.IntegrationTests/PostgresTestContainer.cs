using Testcontainers.PostgreSql;

namespace EventApi.IntegrationTests;

public sealed class PostgresTestContainer : IAsyncLifetime
{
    private const string DatabaseName = "event_service_tests";
    private const string Username = "postgres";
    private const string Password = "postgres";

    private PostgreSqlContainer _container = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase(DatabaseName)
        .WithUsername(Username)
        .WithPassword(Password)
        .Build();

        await _container.StartAsync();
    }

    public Task DisposeAsync()
    {
        return _container.DisposeAsync().AsTask();
    }
}