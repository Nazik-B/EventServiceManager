using BookingsService.Domain.Entities;

namespace BookingsService.Application.Repositories;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}