using EventsApi.DataAccess;
using EventsApi.Models;
using Microsoft.EntityFrameworkCore;
using MyWebApiEventSrvManagerProj.Exceptions;

namespace EventsApi.Services;

public class BookingService : IBookingService
{
    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    private readonly AppDbContext _context;

    public BookingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        await BookingSemaphore.WaitAsync();

        try
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventItem is null)
            {
                throw new NotFoundException(
                    $"Event with id {eventId} was not found");
            }

            var reserved = eventItem.TryReserveSeats();

            if (!reserved)
            {
                throw new NoAvailableSeatsException();
            }

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = null
            };

            _context.Bookings.Add(booking);

            await _context.SaveChangesAsync();

            return booking;
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<Booking?> GetBookingByIdAsync(Guid bookingId)
    {
        return await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId);
    }

    public async Task<IEnumerable<Booking>> GetPendingBookingsAsync(
        CancellationToken cancellationToken)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateBookingStatusAsync(
        Guid bookingId,
        BookingStatus status,
        DateTime processedAt,
        CancellationToken cancellationToken)
    {
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(
                b => b.Id == bookingId,
                cancellationToken);

        if (booking is null)
        {
            return;
        }

        booking.Status = status;
        booking.ProcessedAt = processedAt;

        await _context.SaveChangesAsync(cancellationToken);
    }
}