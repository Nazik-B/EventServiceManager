using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;
using Xunit;

namespace EventsApi.UnitTests;

public class UserTests
{
    [Fact]
    public void Create_SetsFieldsAndGeneratesId()
    {
        var user = User.Create("alice", "hash", Role.Admin);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("alice", user.Login);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(Role.Admin, user.Role);
    }

    [Fact]
    public void Create_UsesUserRole_ByDefault()
    {
        var user = User.Create("alice", "hash");

        Assert.Equal(Role.User, user.Role);
    }

    [Fact]
    public void Create_TrimsLogin()
    {
        var user = User.Create("  alice  ", "hash");

        Assert.Equal("alice", user.Login);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Throws_WhenLoginIsBlank(string? login)
    {
        Assert.Throws<DomainValidationException>(
            () => User.Create(login!, "hash"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Throws_WhenPasswordHashIsBlank(string? passwordHash)
    {
        Assert.Throws<DomainValidationException>(
            () => User.Create("alice", passwordHash!));
    }
}
