using System.Security.Cryptography;
using System.Text;
using EventsApi.Application.Abstractions;

namespace EventsApi.Infrastructure.Security;

public sealed class Sha256PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            return false;

        var actual = Encoding.UTF8.GetBytes(Hash(password));
        var expected = Encoding.UTF8.GetBytes(hash.ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
