using Microsoft.EntityFrameworkCore;
using EventsApi.DataAccess;
using EventsApi.Models;
using EventsApi.Repositories.Interfaces;

namespace EventsApi.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _dbContext;

    public BookingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Booking?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Bookings
            .AsNoTracking()
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetByEventIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.EventId == eventId)
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Booking entity,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Bookings.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(
        Booking entity,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Bookings.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        Booking entity,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Bookings.Remove(entity);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Bookings.AnyAsync(b => b.Id == id, cancellationToken);
    }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
