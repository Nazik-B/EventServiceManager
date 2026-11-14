using EventsApi.Infrastructure.Security;
using Xunit;

namespace EventsApi.UnitTests;

public class Sha256PasswordHasherTests
{
    private readonly Sha256PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ReturnsSame64CharHex_ForSamePassword()
    {
        var a = _hasher.Hash("secret");
        var b = _hasher.Hash("secret");

        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void Hash_DiffersForDifferentPasswords() =>
        Assert.NotEqual(_hasher.Hash("a"), _hasher.Hash("b"));

    [Fact]
    public void Hash_MatchesKnownSha256Value() =>
        Assert.Equal(
            "2BB80D537B1DA3E38BD30361AA855686BDE0EACD7162FEF6A25FE97BF527A25B",
            _hasher.Hash("secret"));

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword() =>
        Assert.True(_hasher.Verify("secret", _hasher.Hash("secret")));

    [Fact]
    public void Verify_ReturnsTrue_ForLowercaseHash() =>
        Assert.True(_hasher.Verify("secret", _hasher.Hash("secret").ToLowerInvariant()));

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword() =>
        Assert.False(_hasher.Verify("wrong", _hasher.Hash("secret")));

    [Theory]
    [InlineData("", "abc")]
    [InlineData("secret", "")]
    public void Verify_ReturnsFalse_ForEmptyInput(string password, string hash) =>
        Assert.False(_hasher.Verify(password, hash));

    [Fact]
    public void Hash_Throws_ForEmptyPassword() =>
        Assert.Throws<ArgumentException>(() => _hasher.Hash(""));
}
