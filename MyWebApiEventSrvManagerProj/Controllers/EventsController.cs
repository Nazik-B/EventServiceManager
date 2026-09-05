using Microsoft.AspNetCore.Mvc;
using EventsApi.Models;
using EventsApi.Services;

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

    // GET /events
    [HttpGet]
    public ActionResult<IEnumerable<Event>> GetAll()
    {
        return Ok(_eventService.GetAll());
    }

    // GET /events/{id}
    [HttpGet("{id:int}")]
    public ActionResult<Event> GetById(int id)
    {
        var eventItem = _eventService.GetById(id);
        if (eventItem is null)
            return NotFound();

        return Ok(eventItem);
    }

    // POST /events
    [HttpPost]
    public ActionResult<Event> Create([FromBody] Event eventItem)
    {
        var created = _eventService.Create(eventItem);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT /events/{id}
    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] Event eventItem)
    {
        var updated = _eventService.Update(id, eventItem);
        if (!updated)
            return NotFound();

        return NoContent();
    }

    // DELETE /events/{id}
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var deleted = _eventService.Delete(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }
}