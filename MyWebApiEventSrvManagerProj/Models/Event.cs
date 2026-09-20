using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EventsApi.Models;

public class Event : IValidatableObject
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Title is required")]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required(ErrorMessage = "StartAt is required")]
    public DateTime StartAt { get; set; }

    [Required(ErrorMessage = "EndAt is required")]
    public DateTime EndAt { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "TotalSeats must be greater than zero")]
    public int TotalSeats { get; set; }
    public int AvailableSeats  { get; private set; }

    public ICollection<Booking> Bookings { get; private set; } = new List<Booking>();

    private Event()
    {
        
    }

    [SetsRequiredMembers]
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
        {
            throw new ValidationException(
                "TotalSeats must be greater than zero");
        }

        return new Event(
            id,
            title,
            description,
            startAt,
            endAt,
            totalSeats);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
        {
            yield return new ValidationResult(
                "EndAt must be later than StartAt",
                new[] { nameof(EndAt) });
        }
    }

    public bool TryReserveSeats(int count = 1)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Seat count must be greater than zero.");
        }

        if (AvailableSeats < count)
        {
            return false;
        }

        AvailableSeats -= count;

        return true;
    }

     public void ReleaseSeats(int count = 1)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                "Seat count must be greater than zero.");
        }

        if (AvailableSeats + count > TotalSeats)
        {
            throw new ValidationException(
                "Cannot release more seats than the event total capacity.");
        }

        AvailableSeats += count;
    }
}