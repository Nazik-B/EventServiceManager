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