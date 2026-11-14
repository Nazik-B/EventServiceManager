namespace EventsApi.Domain.Exceptions;

public class ForbiddenOperationException : Exception
{
    public ForbiddenOperationException()
        : base("You do not have permission to perform this operation")
    {
    }

    public ForbiddenOperationException(string message) : base(message) { }
}
