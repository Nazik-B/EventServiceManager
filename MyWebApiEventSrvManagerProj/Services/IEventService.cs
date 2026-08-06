using EventsApi.Models.Dto;

namespace EventsApi.Services;

public interface IEventService
{
    PaginatedResult<EventResponse> GetAll(string? title, DateTime? from, DateTime? to, int page, int pageSize);
    EventResponse? GetById(int id);
    EventResponse Create(CreateEventRequest request);
    bool Update(int id, UpdateEventRequest request);
    bool Delete(int id);
    
}