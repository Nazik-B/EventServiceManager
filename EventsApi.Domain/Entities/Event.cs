using EventsApi.Domain.Exceptions;

namespace EventsApi.Domain.Entities;

public class Event
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; private set; }

    public ICollection<Booking> Bookings { get; private set; } = new List<Booking>();

    private Event() { }

    private Event(Guid id, string title, string? description, DateTime startAt, DateTime endAt, int totalSeats)
    {
        Id = id;
        Title = title;
        Description = description;
        StartAt = startAt;
        EndAt = endAt;
        TotalSeats = totalSeats;
        AvailableSeats = totalSeats;
    }

    public static Event Create(Guid id, string title, string? description, DateTime startAt, DateTime endAt, int totalSeats)
    {
        if (totalSeats <= 0)
            throw new DomainValidationException("TotalSeats must be greater than zero");

        return new Event(id, title, description, startAt, endAt, totalSeats);
    }

    public bool TryReserveSeats(int count = 1)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Seat count must be greater than zero.");

        if (AvailableSeats < count)
            return false;

        AvailableSeats -= count;
        return true;
    }

    public void ReleaseSeats(int count = 1)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Seat count must be greater than zero.");

        if (AvailableSeats + count > TotalSeats)
            throw new DomainValidationException("Cannot release more seats than the event total capacity.");

        AvailableSeats += count;
    }
}