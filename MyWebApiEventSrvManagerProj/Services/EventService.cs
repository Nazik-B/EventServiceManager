using EventsApi.Models;
using EventsApi.Models.Dto;

namespace EventsApi.Services;

public class EventService : IEventService
{
    private readonly List<Event> _events = new();

    public IEnumerable<EventResponse> GetAll(string? title, DateTime? from, DateTime? to)
    {
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

        return query.Select(MapToResponse).ToList();
    }        

    public EventResponse? GetById(int id)
    {
        var existing = _events.FirstOrDefault(e => e.Id == id);
        return existing is null ? null : MapToResponse(existing);
    }

    public EventResponse Create(CreateEventRequest request)
    {
        var eventItem = new Event
        {
            Id = _events.Count == 0 ? 1 : _events.Max(e => e.Id) + 1,
            Title = request.Title,
            Description = request.Description,
            StartAt = request.StartAt!.Value,
            EndAt = request.EndAt!.Value
        };

        _events.Add(eventItem);
        return MapToResponse(eventItem);
    }

    public bool Update(int id, UpdateEventRequest request)
    {
        var existing = _events.FirstOrDefault(e => e.Id == id);
        if (existing is null)
            return false;

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.StartAt = request.StartAt!.Value;
        existing.EndAt = request.EndAt!.Value;
        return true;
    }

    public bool Delete(int id)
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
        EndAt = e.EndAt
    };
}