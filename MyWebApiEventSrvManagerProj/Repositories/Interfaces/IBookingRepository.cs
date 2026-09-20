using EventsApi.Models;

namespace EventsApi.Repositories.Interfaces;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Booking>> GetByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Booking entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(Booking entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(Booking entity, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
