using BookingsService.Application.Dto;
using BookingsService.Application.Repositories;
using BookingsService.Domain.Entities;
using BookingsService.Domain.Exceptions;

namespace BookingsService.Application.Services;

public class BookingService : IBookingService
{
    private const int MaxActiveBookingsPerUser = 10;

    private static readonly SemaphoreSlim BookingSemaphore = new(1, 1);

    private readonly IBookingRepository _bookingRepository;

    public BookingService(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    public async Task<BookingResponse> CreateBookingAsync(Guid eventId, Guid userId)
    {
        await BookingSemaphore.WaitAsync();

        try
        {
            var activeCount = await _bookingRepository.CountActiveByUserAsync(userId);

            if (activeCount >= MaxActiveBookingsPerUser)
            {
                throw new ActiveBookingsLimitExceededException(MaxActiveBookingsPerUser);
            }

            var booking = Booking.Create(eventId, userId);

            await _bookingRepository.AddAsync(booking);
            await _bookingRepository.SaveChangesAsync();

            return ToResponse(booking);
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    public async Task<BookingResponse?> GetBookingByIdAsync(Guid bookingId, Guid userId, bool isAdmin)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);

        if (booking is null)
        {
            return null;
        }

        if (booking.UserId != userId && !isAdmin)
        {
            throw new ForbiddenOperationException("You can view only your own bookings");
        }

        return ToResponse(booking);
    }

    public async Task<IEnumerable<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        return await _bookingRepository.GetPendingAsync(cancellationToken);
    }

    public async Task UpdateBookingStatusAsync(
        Guid bookingId,
        BookingStatus status,
        DateTime processedAt,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdTrackedAsync(bookingId, cancellationToken);

        if (booking is null)
        {
            return;
        }

        booking.Status = status;
        booking.ProcessedAt = processedAt;

        await _bookingRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<BookingResponse> CancelBookingAsync(
        Guid bookingId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        await BookingSemaphore.WaitAsync(cancellationToken);

        try
        {
            var booking = await _bookingRepository.GetByIdTrackedAsync(bookingId, cancellationToken);

            if (booking is null)
            {
                throw new NotFoundException($"Booking with id {bookingId} was not found");
            }

            if (booking.UserId != userId && !isAdmin)
            {
                throw new ForbiddenOperationException("You can cancel only your own bookings");
            }

            booking.Cancel();

            await _bookingRepository.SaveChangesAsync(cancellationToken);

            return ToResponse(booking);
        }
        finally
        {
            BookingSemaphore.Release();
        }
    }

    private static BookingResponse ToResponse(Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        Status = booking.Status,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };
}
