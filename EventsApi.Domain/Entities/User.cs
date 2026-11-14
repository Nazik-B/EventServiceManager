using EventsApi.Domain.Exceptions;

namespace EventsApi.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Login { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Role Role { get; private set; }

    public ICollection<Booking> Bookings { get; private set; } = new List<Booking>();

    private User() { }

    public static User Create(string login, string passwordHash, Role role = Role.User)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new DomainValidationException("Login is required");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainValidationException("PasswordHash is required");

        return new User
        {
            Id = Guid.NewGuid(),
            Login = login.Trim(),
            PasswordHash = passwordHash,
            Role = role
        };
    }
}
