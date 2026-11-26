using EventsService.Application;
using EventsService.Domain.Entities;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EventsService.Infrastructure;

public sealed class ProcessedBookingRepository : IProcessedBookingRepository
{
    private readonly EventsDbContext _db;
    public ProcessedBookingRepository(EventsDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid bookingId, CancellationToken ct) =>
        _db.ProcessedBookings.AnyAsync(x => x.BookingId == bookingId, ct);

    public void Add(Guid bookingId) =>
        _db.ProcessedBookings.Add(new ProcessedBooking { BookingId = bookingId, ProcessedAt = DateTime.UtcNow });
}