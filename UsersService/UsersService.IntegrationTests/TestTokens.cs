using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace UsersService.IntegrationTests;

internal static class TestTokens
{
    public const string Secret = "integration-tests-secret-0123456789abcdef";
    public const string Issuer = "EventsApi";
    public const string Audience = "EventsApi.Clients";

    public static string Create(
        Guid userId,
        string role,
        string secret = Secret,
        string issuer = Issuer,
        TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = Audience,
            NotBefore = now.AddHours(-2),
            Expires = now + (lifetime ?? TimeSpan.FromHours(1)),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(),
                ["unique_name"] = "test-user",
                ["role"] = role
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
