using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EventsApi.Application.Dto;

namespace EventsApi.IntegrationTests;

public sealed class AuthorizationHttpTests : IClassFixture<ApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly ApiFactory _factory;

    public AuthorizationHttpTests(ApiFactory factory) => _factory = factory;

    private sealed record IdDto(Guid Id);

    private static string NewLogin(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..30];

    private async Task<string> RegisterAndLoginAsync(string role)
    {
        var client = _factory.CreateClient();
        var login = NewLogin(role.ToLowerInvariant());

        var register = await client.PostAsJsonAsync("/auth/register",
            new { login, password = Password, role });
        Assert.Equal(HttpStatusCode.NoContent, register.StatusCode);

        var response = await client.PostAsJsonAsync("/auth/login",
            new { login, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    private HttpClient ClientWithToken(string? token)
    {
        var client = _factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static object NewEvent() => new
    {
        title = "HTTP test event",
        startAt = DateTime.UtcNow.AddDays(30),
        endAt = DateTime.UtcNow.AddDays(30).AddHours(1),
        totalSeats = 5
    };

    [Fact]
    public async Task PostEvents_WithoutToken_Returns401()
    {
        var response = await ClientWithToken(null).PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithUserToken_Returns403()
    {
        var token = await RegisterAndLoginAsync("User");

        var response = await ClientWithToken(token).PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostEvents_WithAdminToken_Returns201()
    {
        var token = await RegisterAndLoginAsync("Admin");

        var response = await ClientWithToken(token).PostAsJsonAsync("/events", NewEvent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task BookEvent_WithoutToken_Returns401()
    {
        var response = await ClientWithToken(null)
            .PostAsync($"/events/{Guid.NewGuid()}/book", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBooking_WithGarbageToken_Returns401()
    {
        var response = await ClientWithToken("not-a-jwt")
            .GetAsync($"/bookings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBooking_WithTamperedSignature_Returns401()
    {
        var token = await RegisterAndLoginAsync("User");
        var index = token.LastIndexOf('.') + 5;
        var tampered = token[..index] + (token[index] == 'A' ? 'B' : 'A') + token[(index + 1)..];

        var response = await ClientWithToken(tampered)
            .GetAsync($"/bookings/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBooking_OfAnotherUser_Returns403_ForOwnerAndAdminReturns200()
    {
        var adminToken = await RegisterAndLoginAsync("Admin");
        var ownerToken = await RegisterAndLoginAsync("User");
        var strangerToken = await RegisterAndLoginAsync("User");

        var created = await ClientWithToken(adminToken).PostAsJsonAsync("/events", NewEvent());
        var eventId = (await created.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var booked = await ClientWithToken(ownerToken)
            .PostAsync($"/events/{eventId}/book", content: null);
        Assert.Equal(HttpStatusCode.Accepted, booked.StatusCode);
        var bookingId = (await booked.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var asStranger = await ClientWithToken(strangerToken).GetAsync($"/bookings/{bookingId}");
        var asOwner = await ClientWithToken(ownerToken).GetAsync($"/bookings/{bookingId}");
        var asAdmin = await ClientWithToken(adminToken).GetAsync($"/bookings/{bookingId}");

        Assert.Equal(HttpStatusCode.Forbidden, asStranger.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateLogin_Returns409()
    {
        var client = _factory.CreateClient();
        var login = NewLogin("dup");
        var body = new { login, password = Password };

        var first = await client.PostAsJsonAsync("/auth/register", body);
        var second = await client.PostAsJsonAsync("/auth/register", body);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var login = NewLogin("wrong");
        await client.PostAsJsonAsync("/auth/register", new { login, password = Password });

        var response = await client.PostAsJsonAsync("/auth/login",
            new { login, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}