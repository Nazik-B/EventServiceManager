using System.Text;
using EventsApi.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace EventsApi.UnitTests;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Secret = "unit-test-secret-0123456789-abcdefghijklmnop",
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        LifetimeMinutes = 30
    };

    private readonly JwtTokenService _service = new(Microsoft.Extensions.Options.Options.Create(Options));

    private static TokenValidationParameters Parameters(string secret) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Options.Issuer,
        ValidateAudience = true,
        ValidAudience = Options.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ClockSkew = TimeSpan.Zero
    };

    [Fact]
    public void GenerateToken_ReturnsThreePartToken()
    {
        var token = _service.GenerateToken(Guid.NewGuid(), "alice", "Admin");

        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void GenerateToken_ContainsUserClaims()
    {
        var id = Guid.NewGuid();

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(_service.GenerateToken(id, "alice", "Admin"));

        Assert.Equal(id.ToString(), jwt.GetClaim("sub").Value);
        Assert.Equal("alice", jwt.GetClaim("unique_name").Value);
        Assert.Equal("Admin", jwt.GetClaim("role").Value);
    }

    [Fact]
    public void GenerateToken_ContainsIssuerAudienceAndLifetime()
    {
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(_service.GenerateToken(Guid.NewGuid(), "alice", "User"));

        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains("TestAudience", jwt.Audiences);
        Assert.Equal(TimeSpan.FromMinutes(30), jwt.ValidTo - jwt.ValidFrom);
    }

    [Fact]
    public async Task GenerateToken_IsValidWithSameSecret()
    {
        var token = _service.GenerateToken(Guid.NewGuid(), "alice", "User");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters(Options.Secret));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task GenerateToken_IsInvalidWithDifferentSecret()
    {
        var token = _service.GenerateToken(Guid.NewGuid(), "alice", "User");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Parameters("another-secret-0123456789-abcdefghijklmnopqrstu"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GenerateToken_GivesDifferentTokensForEachCall()
    {
        var id = Guid.NewGuid();

        Assert.NotEqual(_service.GenerateToken(id, "alice", "User"), _service.GenerateToken(id, "alice", "User"));
    }

    [Theory]
    [InlineData("", "User")]
    [InlineData("alice", "")]
    public void GenerateToken_Throws_ForEmptyLoginOrRole(string login, string role) =>
        Assert.Throws<ArgumentException>(() => _service.GenerateToken(Guid.NewGuid(), login, role));
}