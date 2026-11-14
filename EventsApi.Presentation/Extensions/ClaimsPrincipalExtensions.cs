using System.Security.Claims;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;

namespace EventsApi.Presentation.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub");

        if (!Guid.TryParse(value, out var userId))
            throw new InvalidCredentialsException();

        return userId;
    }

    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(Role.Admin.ToString());
    }
}
