using EventsApi.Models;
using EventsApi.Models.Dto;
using System.ComponentModel.DataAnnotations;
using MyWebApiEventSrvManagerProj.Exceptions;

namespace EventsApi.Services;

public class EventService : IEventService
{
    private readonly List<Event> _events = new();

    public PaginatedResult<EventResponse> GetAll(string? title, DateTime? from, DateTime? to, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var query = _events.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(e => e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.StartAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.EndAt <= to.Value);
        }

        var totalCount = query.Count();

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToResponse)
            .ToList();

        return new PaginatedResult<EventResponse>
        {
            TotalCount = totalCount,
            Items = items,
            Page = page,
            PageSize = pageSize
        };
    }

    public EventResponse? GetById(Guid id)
    {
        var existing = _events.FirstOrDefault(e => e.Id == id);
        return existing is null ? null : MapToResponse(existing);
    }

    public EventResponse Create(CreateEventRequest request)
    {
        ValidateDates(request.StartAt, request.EndAt);

        var eventItem =  Event.Create(
            Guid.NewGuid(),
            request.Title,
            request.Description,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.TotalSeats!.Value);

        _events.Add(eventItem);

        return MapToResponse(eventItem);
    }

    public bool Update(Guid id, UpdateEventRequest request)
    {
        var existing = _events.FirstOrDefault(e => e.Id == id);
        if (existing is null)
            return false;

        ValidateDates(request.StartAt, request.EndAt);

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.StartAt = request.StartAt!.Value;
        existing.EndAt = request.EndAt!.Value;
        return true;
    }

    public bool Delete(Guid id)
    {
        var existing = _events.FirstOrDefault(e => e.Id == id);
        if (existing is null)
            return false;

        _events.Remove(existing);
        return true;
    }

    private static EventResponse MapToResponse(Event e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        StartAt = e.StartAt,
        EndAt = e.EndAt,
        TotalSeats = e.TotalSeats,
        AvailableSeats = e.AvailableSeats
    };

    private static void ValidateDates(DateTime? startAt, DateTime? endAt)
    {
        if (startAt.HasValue && endAt.HasValue && endAt <= startAt)
        {
            throw new ValidationException("EndAt must be later than StartAt");
        }
    }

    public bool TryReserveSeat(Guid eventId)
    {
        var eventItem = _events.FirstOrDefault(
            e => e.Id == eventId);

        if (eventItem is null)
        {
            throw new NotFoundException(
                $"Event with id {eventId} was not found");
        }

        return eventItem.TryReserveSeats();
    }
}