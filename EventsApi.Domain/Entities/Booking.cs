using EventsApi.Domain.Exceptions;

namespace EventsApi.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    private Booking()
    {
    }

    public static Booking Create(Guid eventId, Guid userId)
    {
        if (userId == Guid.Empty)
            throw new DomainValidationException("UserId is required");

        return new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };
    }
}
