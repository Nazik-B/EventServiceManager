using System.ComponentModel.DataAnnotations;
using EventsApi.Domain.Entities;

namespace EventsApi.Application.Dto;

public class RegisterRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Login { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; init; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    public string Login { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public class UserResponse
{
    public Guid Id { get; init; }
    public string Login { get; init; } = string.Empty;
    public Role Role { get; init; }
}

public class LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
}
