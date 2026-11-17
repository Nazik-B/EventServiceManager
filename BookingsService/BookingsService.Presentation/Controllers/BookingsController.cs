using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookingsService.Application.Dto;
using BookingsService.Application.Services;
using BookingsService.Domain.Exceptions;
using BookingsService.Presentation.Extensions;

namespace BookingsService.Presentation.Controllers;

[ApiController]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    // POST /events/{eventId}/book
    [Authorize]
    [HttpPost("events/{eventId:guid}/book")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Book(Guid eventId)
    {
        var booking = await _bookingService.CreateBookingAsync(eventId, User.GetUserId());

        return Accepted($"/bookings/{booking.Id}", booking);
    }

    // GET /bookings/{id}
    [Authorize]
    [HttpGet("bookings/{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> GetById(Guid id)
    {
        var booking = await _bookingService.GetBookingByIdAsync(
            id,
            User.GetUserId(),
            isAdmin: User.IsAdmin());

        if (booking is null)
        {
            throw new NotFoundException($"Booking with id {id} was not found");
        }

        return Ok(booking);
    }

    // DELETE /bookings/{id}
    [Authorize]
    [HttpDelete("bookings/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _bookingService.CancelBookingAsync(
            id,
            User.GetUserId(),
            isAdmin: User.IsAdmin(),
            cancellationToken);

        return NoContent();
    }
}
