namespace EventsApi.Domain.Entities;

/// <summary>
/// Default user that owns bookings created before authentication was introduced.
/// Seeded by the AddUsers migration with the same id.
/// </summary>
public static class SystemUser
{
    public const string Login = "system";

    public static readonly Guid Id = new("00000000-0000-0000-0000-000000000001");
}
