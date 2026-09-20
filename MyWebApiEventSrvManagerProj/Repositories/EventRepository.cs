using EventsApi.DataAccess;
using EventsApi.Models;
using EventsApi.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventsApi.Repositories;

public sealed class EventRepository : IEventRepository
{
    private readonly AppDbContext _dbContext;

    public EventRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
        string? title,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize)
    {
        IQueryable<Event> query = _dbContext.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var normalizedTitle = title.ToLower();

            query = query.Where(e =>
                e.Title.ToLower().Contains(normalizedTitle));
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
            .OrderBy(e => e.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<Event?> GetByIdAsync(Guid id)
    {
        return _dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public Task<Event?> GetByIdForUpdateAsync(Guid id)
    {
        return _dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task AddAsync(Event eventItem)
    {
        await _dbContext.Events.AddAsync(eventItem);
    }

    public void Delete(Event eventItem)
    {
        _dbContext.Events.Remove(eventItem);
    }

    public Task<int> SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}