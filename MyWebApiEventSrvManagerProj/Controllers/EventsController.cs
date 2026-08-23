using Microsoft.AspNetCore.Mvc;
using EventsApi.Models.Dto;
using EventsApi.Services;
using MyWebApiEventSrvManagerProj.Exceptions;

namespace EventsApi.Controllers;

[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly IBookingService _bookingService;

    public EventsController(IEventService eventService, IBookingService bookingService)
    {
        _eventService = eventService;
        _bookingService = bookingService;
    }

    // GET /events?title=...&from=...&to=...&page=1&pageSize=10
    [HttpGet]
    public ActionResult<PaginatedResult<EventResponse>> GetAll(
        [FromQuery] string? title,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = _eventService.GetAll(title, from, to, page, pageSize);
        return Ok(result);
    }

    // GET /events/{id}
    [HttpGet("{id:guid}")]
    public ActionResult<EventResponse> GetById(Guid id)
    {
        var eventItem = _eventService.GetById(id);
        if (eventItem is null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        return Ok(eventItem);
    }

    // POST /events
    [HttpPost]
    public ActionResult<EventResponse> Create([FromBody] CreateEventRequest request)
    {
        var created = _eventService.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /events/{id}
    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var updated = _eventService.Update(id, request);
        if (!updated)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        return NoContent();
    }

    // DELETE /events/{id}
    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        var deleted = _eventService.Delete(id);
        if (!deleted)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        return NoContent();
    }

    // POST /events/{id}/book
    [HttpPost("{id:guid}/book")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Book(Guid id)
    {
        var booking = await _bookingService.CreateBookingAsync(id);

        var response = new BookingResponse
        {
            Id = booking.Id,
            EventId = booking.EventId,
            Status = booking.Status,
            CreatedAt = booking.CreatedAt,
            ProcessedAt = booking.ProcessedAt
        };

        return Accepted($"/bookings/{booking.Id}", response);
    }
}