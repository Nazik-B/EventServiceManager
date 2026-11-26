using BookingsService.Application.Repositories;
using BookingsService.Domain.Entities;
using BookingsService.Infrastructure.DataAccess;

namespace BookingsService.Infrastructure.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly BookingsDbContext _dbContext;

    public OutboxRepository(BookingsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }
}