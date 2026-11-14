namespace EventsApi.Domain.Exceptions;

public class ActiveBookingsLimitExceededException : Exception
{
    public ActiveBookingsLimitExceededException(int limit)
        : base($"Active bookings limit ({limit}) has been exceeded")
    {
    }
}
