using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EventsService.IntegrationTests;

public sealed class EventsHttpTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public EventsHttpTests(ApiFactory factory) => _factory = factory;

    private sealed record IdDto(Guid Id);

    private HttpClient Client(string? token = null)
    {
        var client = _factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string UserToken() => TestTokens.Create(Guid.NewGuid(), "User");
    private static string AdminToken() => TestTokens.Create(Guid.NewGuid(), "Admin");

    private static object NewEvent() => new
    {
        title = "HTTP test event",
        startAt = DateTime.UtcNow.AddDays(30),
        endAt = DateTime.UtcNow.AddDays(30).AddHours(1),
        totalSeats = 5
    };

    [Fact]
    public async Task GetEvents_WithoutToken_Returns200()
    {
        var response = await Client().GetAsync("/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithoutToken_Returns401()
    {
        var response = await Client().PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithUserToken_Returns403()
    {
        var response = await Client(UserToken()).PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithAdminToken_Returns201()
    {
        var response = await Client(AdminToken()).PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithGarbageToken_Returns401()
    {
        var response = await Client("not-a-jwt").PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithTamperedSignature_Returns401()
    {
        var token = AdminToken();
        var index = token.LastIndexOf('.') + 5;
        var tampered = token[..index] + (token[index] == 'A' ? 'B' : 'A') + token[(index + 1)..];

        var response = await Client(tampered).PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithTokenSignedByAnotherSecret_Returns401()
    {
        var token = TestTokens.Create(Guid.NewGuid(), "Admin", secret: "another-secret-0123456789abcdef-xxxx");

        var response = await Client(token).PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithWrongIssuer_Returns401()
    {
        var token = TestTokens.Create(Guid.NewGuid(), "Admin", issuer: "SomeoneElse");

        var response = await Client(token).PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithExpiredToken_Returns401()
    {
        var token = TestTokens.Create(Guid.NewGuid(), "Admin", lifetime: TimeSpan.FromHours(-1));

        var response = await Client(token).PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_UserForbidden_AdminDeletes_ThenGetReturns404()
    {
        var created = await Client(AdminToken()).PostAsJsonAsync("/events", NewEvent());
        var id = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var asUser = await Client(UserToken()).DeleteAsync($"/events/{id}");
        var asAdmin = await Client(AdminToken()).DeleteAsync($"/events/{id}");
        var afterDelete = await Client().GetAsync($"/events/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, asAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }
}