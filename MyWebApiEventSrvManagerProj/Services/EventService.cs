using EventsApi.Models;

namespace EventsApi.Services;

public class EventService : IEventService
{
    private readonly List<Event> _events = new();

    public IEnumerable<Event> GetAll() => _events;

    public Event? GetById(int id) =>
        _events.FirstOrDefault(e => e.Id == id);

    public Event Create(Event eventItem)
    {
        eventItem.Id = _events.Count == 0 ? 1 : _events.Max(e => e.Id) + 1;
        _events.Add(eventItem);
        return eventItem;
    }
}