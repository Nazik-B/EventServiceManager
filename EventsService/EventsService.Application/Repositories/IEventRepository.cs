using EventsService.Domain.Entities;

namespace EventsService.Application.Repositories;

public interface IEventRepository
{
    Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
        string? title,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize);

    Task<Event?> GetByIdAsync(Guid id);

    Task<Event?> GetByIdTrackedAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Event eventItem);

    void Delete(Event eventItem);

    Task<IReadOnlyList<Event>> GetTopBySalesAsync(int count, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync();
}