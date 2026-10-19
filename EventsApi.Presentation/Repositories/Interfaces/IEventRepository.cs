using EventsApi.Models;

namespace EventsApi.Repositories.Interfaces;

public interface IEventRepository
{
    Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
        string? title,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize);

    Task<Event?> GetByIdAsync(Guid id);

    Task<Event?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Event eventItem);

    void Delete(Event eventItem);

    Task<int> SaveChangesAsync();
}