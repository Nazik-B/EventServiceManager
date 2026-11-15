using Xunit;

namespace EventsService.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresTestContainer>
{
    public const string Name = "Postgres";
}
