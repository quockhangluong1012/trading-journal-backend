using System.Security.Claims;

namespace TradingJournal.Shared.Extensions;

public static class CurrentUserExtensions
{
    public static int GetCurrentUserId(this ClaimsPrincipal principal)
    {
        string? userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("UserId")?.Value;
        return int.TryParse(userIdClaim, out var id) ? id : 0;
    }
}
