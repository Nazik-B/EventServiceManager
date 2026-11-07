using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventsApi.Presentation.Extensions;
using EventsApi.Application.Dto;
using EventsApi.Application.Services;
using EventsApi.Domain.Exceptions;

namespace EventsApi.Controllers;

[ApiController]
[Route("bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    // POST /bookings/{id}/cancel
    [Authorize]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        // Identity and role come from the validated JWT
        var cancelled = await _bookingService.CancelBookingAsync(
            id,
            User.GetUserId(),
            isAdmin: User.IsAdmin(),
            cancellationToken);

        return Ok(cancelled);
    }
    // GET /bookings/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingResponse>> GetById(Guid id)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id);
        if (booking is null)
        {
            throw new NotFoundException($"Booking with id {id} was not found");
        }

        return Ok(booking);
    }

}