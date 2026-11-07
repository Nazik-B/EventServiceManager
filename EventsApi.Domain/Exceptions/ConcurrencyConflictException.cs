namespace EventsApi.Domain.Exceptions;

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The resource was modified by another request. Please retry.")
    {
    }

    public ConcurrencyConflictException(Exception innerException)
        : base("The resource was modified by another request. Please retry.", innerException)
    {
    }
}
