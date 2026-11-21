using BookingsService.Domain.Exceptions;

namespace BookingsService.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    private Booking()
    {
    }

    public static Booking Create(Guid eventId, Guid userId)
    {
        if (eventId == Guid.Empty)
            throw new DomainValidationException("EventId is required");

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

    public void Cancel()
    {
        if (Status == BookingStatus.Cancelled)
            throw new DomainValidationException("Booking is already cancelled");

        if (Status == BookingStatus.Rejected)
            throw new DomainValidationException("Rejected booking cannot be cancelled");

        Status = BookingStatus.Cancelled;
    }
}
