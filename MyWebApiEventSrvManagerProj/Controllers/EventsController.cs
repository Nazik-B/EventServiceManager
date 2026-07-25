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

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    // GET /events?title=...&from=...&to=...
    [HttpGet]
    public ActionResult<IEnumerable<EventResponse>> GetAll(
        [FromQuery] string? title,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var events = _eventService.GetAll(title, from, to);
        return Ok(events);
    }

    // GET /events/{id}
    [HttpGet("{id:int}")]
    public ActionResult<EventResponse> GetById(int id)
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
    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] UpdateEventRequest request)
    {
        var updated = _eventService.Update(id, request);
        if (!updated)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        return NoContent();
    }

    // DELETE /events/{id}
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var deleted = _eventService.Delete(id);
        if (!deleted)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        return NoContent();
    }

        // GET /events/test-error — для тестирования middleware
    /*[HttpGet("test-error")]
    public IActionResult TestError()
    {
        throw new InvalidOperationException("Тестовое исключение для проверки middleware");
    }
    */
}