namespace EventsApi.Application.Abstractions;

public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string login, string role);
}