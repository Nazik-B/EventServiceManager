using EventsApi.Models;
using EventsApi.Models.Dto;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MyWebApiEventSrvManagerProj.Exceptions;
using EventsApi.DataAccess;
using System.Threading;

namespace EventsApi.Services;

public class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<EventResponse>> GetAllAsync(string? title, DateTime? from, DateTime? to, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        IQueryable<Event> query = _context.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(title))
        {
           query = query.Where(e => e.Title.Contains(title));
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.StartAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.EndAt <= to.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventResponse
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                StartAt = e.StartAt,
                EndAt = e.EndAt,
                TotalSeats = e.TotalSeats,
                AvailableSeats = e.AvailableSeats
            })
            .ToListAsync();

        return new PaginatedResult<EventResponse>
        {
            TotalCount = totalCount,
            Items = items,
            Page = page,
            PageSize = pageSize
        };
    }
    public async Task<EventResponse?> GetByIdAsync(Guid id)
    {
        var eventItem = await _context.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        return eventItem is null ? null : MapToResponse(eventItem);
    }
    public async Task<EventResponse> CreateAsync(CreateEventRequest request)
    {
        ValidateDates(request.StartAt, request.EndAt);

        var eventItem =  Event.Create(
            Guid.NewGuid(),
            request.Title,
            request.Description,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.TotalSeats!.Value);

        _context.Events.Add(eventItem);

        await _context.SaveChangesAsync();

        return MapToResponse(eventItem);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateEventRequest request)
    {
        var existing = await _context.Events.FindAsync(id);
        if (existing is null)
            return false;

        ValidateDates(request.StartAt, request.EndAt);

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.StartAt = request.StartAt!.Value;
        existing.EndAt = request.EndAt!.Value;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var existing = await _context.Events.FindAsync(id);
        if (existing is null)
            return false;

        _context.Events.Remove(existing);

        await _context.SaveChangesAsync();
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
            throw new ValidationException("EndAt must be later than StartAt");
        }
    }

    public async Task<bool> TryReserveSeatAsync(Guid eventId)
    {
        var eventItem = await _context.Events.FindAsync(eventId);

        if (eventItem is null)
        {
            throw new NotFoundException(
                $"Event with id {eventId} was not found");
        }

        var reserved = eventItem.TryReserveSeats();

        if (reserved)
        {
            await _context.SaveChangesAsync();
        }

        return reserved;
    }
    public async Task<bool> ReleaseSeatAsync(Guid eventId)
    {
        var eventItem = await _context.Events.FindAsync(eventId);

        if (eventItem is null)
        {
            return false;
        }

        eventItem.ReleaseSeats();

        await _context.SaveChangesAsync();

        return true;
    }
}