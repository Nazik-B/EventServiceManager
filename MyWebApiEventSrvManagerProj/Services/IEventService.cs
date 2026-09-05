using EventsApi.Models;
using EventsApi.Models.Dto;

namespace EventsApi.Services;

public interface IEventService
{
    PaginatedResult<EventResponse> GetAll(string? title, DateTime? from, DateTime? to, int page, int pageSize);
    EventResponse? GetById(Guid id);
    EventResponse Create(CreateEventRequest request);
    bool Update(Guid id, UpdateEventRequest request);
    bool Delete(Guid id);
    bool TryReserveSeat(Guid eventId);
    bool ReleaseSeat(Guid eventId);
}