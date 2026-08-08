using System.ComponentModel.DataAnnotations;

namespace EventsApi.Models;

public class Event : IValidatableObject
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Title is required")]
    public required string Title { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessage = "StartAt is required")]
    public DateTime StartAt { get; set; }

    [Required(ErrorMessage = "EndAt is required")]
    public DateTime EndAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
        {
            yield return new ValidationResult(
                "EndAt must be later than StartAt",
                new[] { nameof(EndAt) });
        }
    }
}