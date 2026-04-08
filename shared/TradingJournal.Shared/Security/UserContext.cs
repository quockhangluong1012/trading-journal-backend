using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TradingJournal.Shared.Security;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public int UserId
    {
        get
        {
            var claims = httpContextAccessor.HttpContext?.User;
            if (claims == null) return 0;

            string? userIdClaim = claims.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? claims.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var id) ? id : 0;
        }
    }

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
