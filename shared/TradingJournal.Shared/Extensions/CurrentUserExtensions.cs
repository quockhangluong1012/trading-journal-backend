using System.Security.Claims;
using TradingJournal.Shared.Exceptions;

namespace TradingJournal.Shared.Extensions;

public static class CurrentUserExtensions
{
    public static int GetCurrentUserId(this ClaimsPrincipal principal)
    {
        string? userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("UserId")?.Value;

        if (!int.TryParse(userIdClaim, out var id) || id <= 0)
        {
            throw new AccessDeniedException("Valid user identity could not be determined.");
        }

        return id;
    }
}
