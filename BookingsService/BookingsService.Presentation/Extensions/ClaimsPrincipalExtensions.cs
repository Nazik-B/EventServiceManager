using System.Security.Claims;
using BookingsService.Domain.Exceptions;

namespace BookingsService.Presentation.Extensions;

public static class ClaimsPrincipalExtensions
{
    private const string AdminRole = "Admin";

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub");

        if (!Guid.TryParse(value, out var userId))
            throw new InvalidCredentialsException();

        return userId;
    }

    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        return principal.IsInRole(AdminRole);
    }
}
