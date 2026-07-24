using EventsApi.Models.Dto;

namespace EventsApi.Services;

public interface IEventService
{
    IEnumerable<EventResponse> GetAll();
    EventResponse? GetById(int id);
    EventResponse Create(CreateEventRequest request);
    bool Update(int id, UpdateEventRequest request);
    bool Delete(int id);
}