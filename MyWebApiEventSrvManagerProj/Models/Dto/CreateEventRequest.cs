using System.ComponentModel.DataAnnotations;

namespace EventsApi.Models.Dto;

public class CreateEventRequest : IValidatableObject
{
    [Required(ErrorMessage = "Title is required")]
    public required string Title { get; set; }

    public string? Description { get; set; }

    [Required(ErrorMessage = "StartAt is required")]
    public DateTime? StartAt { get; set; }

    [Required(ErrorMessage = "EndAt is required")]
    public DateTime? EndAt { get; set; }

    [Required(ErrorMessage = "TotalSeats is required")]
    [Range(1, int.MaxValue, ErrorMessage = "TotalSeats must be greater than zero")]
    public int? TotalSeats { get; set; }
  
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartAt.HasValue && EndAt.HasValue && EndAt <= StartAt)
        {
            yield return new ValidationResult(
                "EndAt must be later than StartAt",
                new[] { nameof(EndAt) });
        }
    }
}