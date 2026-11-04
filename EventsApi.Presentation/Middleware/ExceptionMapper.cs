using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using EventsApi.Domain.Exceptions;

namespace EventsApi.Presentation.Middleware;

public static class ExceptionMapper
{
    public static (int StatusCode, string Title) MapException(Exception exception)
    {
        return exception switch
        {
	    DomainValidationException => (StatusCodes.Status400BadRequest, "Validation error"),            
	    ValidationException => (StatusCodes.Status400BadRequest, "Validation error"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid argument"),
            NoAvailableSeatsException => (StatusCodes.Status409Conflict, "No available seats"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error")
        };
    }
}