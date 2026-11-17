namespace EventServiceManager.Contracts;

public sealed record BookingConfirmed(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int SeatsCount,
    DateTime ConfirmedAt);
