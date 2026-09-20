using System.ComponentModel.DataAnnotations;
using EventsApi.Models;
using EventsApi.Models.Dto;
using MyWebApiEventSrvManagerProj.Exceptions;
using EventsApi.Repositories.Interfaces;

namespace EventsApi.Services;

public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;

    public EventService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<PaginatedResult<EventResponse>> GetAllAsync(
        string? title,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 10;
        }

        var result = await _eventRepository.GetPagedAsync(
            title,
            from,
            to,
            page,
            pageSize);

        return new PaginatedResult<EventResponse>
        {
            TotalCount = result.TotalCount,
            Items = result.Items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<EventResponse?> GetByIdAsync(Guid id)
    {
        var eventItem = await _eventRepository.GetByIdAsync(id);

        return eventItem is null
            ? null
            : MapToResponse(eventItem);
    }

    public async Task<EventResponse> CreateAsync(CreateEventRequest request)
    {
        ValidateDates(request.StartAt, request.EndAt);

        var eventItem = Event.Create(
            Guid.NewGuid(),
            request.Title,
            request.Description,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.TotalSeats!.Value);

        await _eventRepository.AddAsync(eventItem);
        await _eventRepository.SaveChangesAsync();

        return MapToResponse(eventItem);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateEventRequest request)
    {
        var existing = await _eventRepository.GetByIdForUpdateAsync(id);

        if (existing is null)
        {
            return false;
        }

        ValidateDates(request.StartAt, request.EndAt);

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.StartAt = request.StartAt!.Value;
        existing.EndAt = request.EndAt!.Value;

        await _eventRepository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await _eventRepository.GetByIdForUpdateAsync(id);

        if (existing is null)
        {
            return false;
        }

        _eventRepository.Delete(existing);
        await _eventRepository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> TryReserveSeatAsync(Guid eventId)
    {
        var eventItem = await _eventRepository.GetByIdForUpdateAsync(eventId);

        if (eventItem is null)
        {
            throw new NotFoundException(
                $"Event with id {eventId} was not found");
        }

        var reserved = eventItem.TryReserveSeats();

        if (reserved)
        {
            await _eventRepository.SaveChangesAsync();
        }

        return reserved;
    }

    public async Task<bool> ReleaseSeatAsync(Guid eventId)
    {
        var eventItem = await _eventRepository.GetByIdForUpdateAsync(eventId);

        if (eventItem is null)
        {
            return false;
        }

        eventItem.ReleaseSeats();

        await _eventRepository.SaveChangesAsync();

        return true;
    }

    private static EventResponse MapToResponse(Event eventItem) => new()
    {
        Id = eventItem.Id,
        Title = eventItem.Title,
        Description = eventItem.Description,
        StartAt = eventItem.StartAt,
        EndAt = eventItem.EndAt,
        TotalSeats = eventItem.TotalSeats,
        AvailableSeats = eventItem.AvailableSeats
    };

    private static void ValidateDates(DateTime? startAt, DateTime? endAt)
    {
        if (startAt.HasValue && endAt.HasValue && endAt <= startAt)
        {
            throw new ValidationException(
                "EndAt must be later than StartAt");
        }
    }
}