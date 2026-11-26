using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventsService.Application.Dto;
using EventsService.Application.Services;
using EventsService.Domain.Exceptions;

namespace EventsService.Presentation.Controllers;

[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    // GET /events?title=...&from=...&to=...&page=1&pageSize=10
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? title,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _eventService.GetAllAsync(title, from, to, page, pageSize);
        return Ok(result);
    }

    // GET /events/top
    [HttpGet("top")]
    public async Task<IActionResult> GetTop()
    {
        var result = await _eventService.GetTopAsync();
        return Ok(result);
    }

    // GET /events/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var eventItem = await _eventService.GetByIdAsync(id);
        if (eventItem is null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        return Ok(eventItem);
    }

    // POST /events
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
    {
        var created = await _eventService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /events/{id}
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var updated = await _eventService.UpdateAsync(id, request);
        if (!updated)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        return NoContent();
    }

    // DELETE /events/{id}
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _eventService.DeleteAsync(id);
        if (!deleted)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        return NoContent();
    }
}
