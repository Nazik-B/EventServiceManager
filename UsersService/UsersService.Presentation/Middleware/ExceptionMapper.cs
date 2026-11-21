using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using UsersService.Domain.Exceptions;

namespace UsersService.Presentation.Middleware;

public static class ExceptionMapper
{
    public static (int StatusCode, string Title) MapException(Exception exception)
    {
        return exception switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Validation error"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation error"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid argument"),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Invalid credentials"),
            UserAlreadyExistsException => (StatusCodes.Status409Conflict, "Login is already taken"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error")
        };
    }
}