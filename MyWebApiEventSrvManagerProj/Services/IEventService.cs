using EventsApi.Models.Dto;

namespace EventsApi.Services;

public interface IEventService
{
    IEnumerable<EventResponse> GetAll(string? title, DateTime? from, DateTime? to);
    EventResponse? GetById(int id);
    EventResponse Create(CreateEventRequest request);
    bool Update(int id, UpdateEventRequest request);
    bool Delete(int id);
    
}