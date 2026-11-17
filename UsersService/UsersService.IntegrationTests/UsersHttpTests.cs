using System.Net;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;

namespace UsersService.IntegrationTests;

public sealed class UsersHttpTests : IClassFixture<ApiFactory>
{
    private const string Password = "Passw0rd!";

    private readonly ApiFactory _factory;

    public UsersHttpTests(ApiFactory factory) => _factory = factory;

    private sealed record TokenDto(string Token, string TokenType);

    private static string NewLogin(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..30];

    private async Task<string> RegisterAndLoginAsync(string? role)
    {
        var client = _factory.CreateClient();
        var login = NewLogin(role?.ToLowerInvariant() ?? "user");

        var register = await client.PostAsJsonAsync("/auth/register",
            new { login, password = Password, role });
        Assert.Equal(HttpStatusCode.NoContent, register.StatusCode);

        var response = await client.PostAsJsonAsync("/auth/login", new { login, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TokenDto>();
        Assert.Equal("Bearer", body!.TokenType);
        return body.Token;
    }

    [Theory]
    [InlineData(null, "User")]
    [InlineData("User", "User")]
    [InlineData("Admin", "Admin")]
    public async Task Login_ReturnsTokenWithExpectedClaims(string? requestedRole, string expectedRole)
    {
        var token = await RegisterAndLoginAsync(requestedRole);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.True(Guid.TryParse(jwt.Subject, out var userId) && userId != Guid.Empty);
        Assert.Equal(expectedRole, jwt.GetClaim("role").Value);
        Assert.Equal(TestTokens.Issuer, jwt.Issuer);
        Assert.Contains(TestTokens.Audience, jwt.Audiences);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public async Task Register_DuplicateLogin_Returns409()
    {
        var client = _factory.CreateClient();
        var body = new { login = NewLogin("dup"), password = Password };

        var first = await client.PostAsJsonAsync("/auth/register", body);
        var second = await client.PostAsJsonAsync("/auth/register", body);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_WithBlankLogin_Returns400()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/auth/register", new { login = "  ", password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    [Fact]
    public async Task Login_WithUnknownLogin_Returns401()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/auth/login", new { login = NewLogin("nobody"), password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
