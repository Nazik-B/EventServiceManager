namespace EventsApi.Domain.Exceptions;

public class PastEventBookingException : Exception
{
    public PastEventBookingException()
        : base("Cannot book an event that has already started")
    {
    }
}
