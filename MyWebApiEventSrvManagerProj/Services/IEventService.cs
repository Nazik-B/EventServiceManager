using EventsApi.Models;

namespace EventsApi.Services;

public interface IEventService
{
    IEnumerable<Event> GetAll();
    Event? GetById(int id);
    Event Create(Event eventItem);
}