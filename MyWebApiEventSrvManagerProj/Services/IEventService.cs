using EventsApi.Models;

namespace EventsApi.Services;

public interface IEventService
{
    IEnumerable<Event> GetAll();
    Event? GetById(int id);
    Event Create(Event eventItem);
    bool Update(int id, Event eventItem);
    bool Delete(int id);
}