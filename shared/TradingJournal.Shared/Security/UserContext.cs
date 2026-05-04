using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TradingJournal.Shared.Exceptions;

namespace TradingJournal.Shared.Security;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public int UserId
    {
        get
        {
            var claims = httpContextAccessor.HttpContext?.User;

            if (claims is null || claims.Identity?.IsAuthenticated != true)
            {
                throw new AccessDeniedException("User is not authenticated.");
            }

            string? userIdClaim = claims.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? claims.FindFirst("UserId")?.Value;

            if (!int.TryParse(userIdClaim, out var id) || id <= 0)
            {
                throw new AccessDeniedException("Valid user identity could not be determined.");
            }

            return id;
        }
    }

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
