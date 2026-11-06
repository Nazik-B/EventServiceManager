using Microsoft.AspNetCore.Mvc;
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
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        // TODO: use the authenticated user's id and role once JWT auth is added
        var cancelled = await _bookingService.CancelBookingAsync(
            id,
            EventsApi.Domain.Entities.SystemUser.Id,
            isAdmin: false,
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