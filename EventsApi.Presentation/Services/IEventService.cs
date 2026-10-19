using EventsApi.Models.Dto;

namespace EventsApi.Services;

public interface IEventService
{
    Task<PaginatedResult<EventResponse>> GetAllAsync(
        string? title,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize);

    Task<EventResponse?> GetByIdAsync(Guid id);

    Task<EventResponse> CreateAsync(CreateEventRequest request);

    Task<bool> UpdateAsync(Guid id, UpdateEventRequest request);

    Task<bool> DeleteAsync(Guid id);

    Task<bool> TryReserveSeatAsync(Guid eventId);

    Task<bool> ReleaseSeatAsync(Guid eventId);
}