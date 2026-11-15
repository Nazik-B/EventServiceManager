using EventsApi.Application.Abstractions;
using EventsApi.Application.Dto;
using EventsApi.Application.Repositories;
using EventsApi.Application.Services;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;
using EventsApi.Infrastructure.DataAccess;
using EventsApi.Infrastructure.Repositories;
using EventsApi.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventsApi.UnitTests;

public sealed class AuthServiceTests : IDisposable
{
    private const string Password = "Passw0rd!";

    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher = new Sha256PasswordHasher();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IUserRepository, UserRepository>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _users = _scope.ServiceProvider.GetRequiredService<IUserRepository>();
        _auth = new AuthService(_users, _hasher, new FakeJwtTokenService());
    }

    public void Dispose()
    {
        _scope.Dispose();
        _provider.Dispose();
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string GenerateToken(Guid userId, string login, string role) =>
            $"{userId}|{login}|{role}";
    }

    private static RegisterRequest Register(string login, Role? role = null) => new()
    {
        Login = login,
        Password = Password,
        Role = role
    };

    [Fact]
    public async Task RegisterAsync_CreatesUserWithUserRole_ByDefault()
    {
        var response = await _auth.RegisterAsync(Register("alice"));

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("alice", response.Login);
        Assert.Equal(Role.User, response.Role);
    }

    [Fact]
    public async Task RegisterAsync_CreatesAdmin_WhenRoleIsAdmin()
    {
        var response = await _auth.RegisterAsync(Register("boss", Role.Admin));

        Assert.Equal(Role.Admin, response.Role);
    }

    [Fact]
    public async Task RegisterAsync_Throws_ForUnknownRole()
    {
        await Assert.ThrowsAsync<DomainValidationException>(
            () => _auth.RegisterAsync(Register("bob", (Role)999)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_Throws_ForBlankLogin(string login)
    {
        await Assert.ThrowsAsync<DomainValidationException>(
            () => _auth.RegisterAsync(Register(login)));
    }

    [Fact]
    public async Task RegisterAsync_Throws_ForEmptyPassword()
    {
        var request = new RegisterRequest { Login = "carol", Password = string.Empty };

        await Assert.ThrowsAsync<DomainValidationException>(
            () => _auth.RegisterAsync(request));
    }

    [Fact]
    public async Task RegisterAsync_Throws_WhenLoginAlreadyExists()
    {
        await _auth.RegisterAsync(Register("dave"));

        await Assert.ThrowsAsync<UserAlreadyExistsException>(
            () => _auth.RegisterAsync(Register("dave")));
    }

    [Fact]
    public async Task RegisterAsync_TreatsLoginWithSpacesAsSameLogin()
    {
        await _auth.RegisterAsync(Register("erin"));

        await Assert.ThrowsAsync<UserAlreadyExistsException>(
            () => _auth.RegisterAsync(Register("  erin  ")));
    }

    [Fact]
    public async Task RegisterAsync_StoresHash_NotPlainPassword()
    {
        await _auth.RegisterAsync(Register("frank"));

        var user = await _users.GetByLoginAsync("frank");

        Assert.NotNull(user);
        Assert.NotEqual(Password, user!.PasswordHash);
        Assert.True(_hasher.Verify(Password, user.PasswordHash));
    }

    [Fact]
    public async Task LoginAsync_ReturnsToken_ForValidCredentials()
    {
        var registered = await _auth.RegisterAsync(Register("grace", Role.Admin));

        var response = await _auth.LoginAsync(
            new LoginRequest { Login = "grace", Password = Password });

        Assert.Equal($"{registered.Id}|grace|Admin", response.Token);
        Assert.Equal("Bearer", response.TokenType);
    }

    [Fact]
    public async Task LoginAsync_TrimsLogin()
    {
        await _auth.RegisterAsync(Register("heidi"));

        var response = await _auth.LoginAsync(
            new LoginRequest { Login = "  heidi  ", Password = Password });

        Assert.False(string.IsNullOrEmpty(response.Token));
    }

    [Fact]
    public async Task LoginAsync_Throws_ForWrongPassword()
    {
        await _auth.RegisterAsync(Register("ivan"));

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _auth.LoginAsync(new LoginRequest { Login = "ivan", Password = "wrong-password" }));
    }

    [Fact]
    public async Task LoginAsync_Throws_ForUnknownLogin()
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _auth.LoginAsync(new LoginRequest { Login = "nobody", Password = Password }));
    }
}