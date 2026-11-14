namespace EventsApi.Domain.Exceptions;

public class UserAlreadyExistsException : Exception
{
    public UserAlreadyExistsException()
        : base("User with this login already exists")
    {
    }

    public UserAlreadyExistsException(string login)
        : base($"User with login '{login}' already exists")
    {
    }
}
