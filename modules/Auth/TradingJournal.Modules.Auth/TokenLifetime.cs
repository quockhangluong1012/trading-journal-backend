using Microsoft.Extensions.Configuration;

namespace TradingJournal.Modules.Auth;

/// <summary>
/// Centralizes JWT access-token and refresh-token lifetimes so every token-issuing path
/// (user login, staff login, refresh) applies the same policy.
///
/// Access tokens are intentionally short-lived and hard-capped — long sessions are sustained
/// through refresh-token rotation, never by minting a long-lived access token (which cannot
/// be revoked once issued).
/// </summary>
internal static class TokenLifetime
{
    /// <summary>Hard ceiling on the access-token lifetime, regardless of configuration.</summary>
    public const int MaxAccessTokenMinutes = 60;

    private const int MinAccessTokenMinutes = 5;
    private const int DefaultAccessTokenMinutes = 60;
    private const int DefaultRefreshTokenDays = 7;
    private const int DefaultRememberMeRefreshTokenDays = 30;

    /// <summary>
    /// Access-token lifetime in minutes, read from <c>Jwt:ExpiryMinutes</c> and clamped to
    /// [<see cref="MinAccessTokenMinutes"/>, <see cref="MaxAccessTokenMinutes"/>].
    /// </summary>
    public static int AccessTokenMinutes(IConfiguration configuration) =>
        Math.Clamp(
            configuration.GetValue("Jwt:ExpiryMinutes", DefaultAccessTokenMinutes),
            MinAccessTokenMinutes,
            MaxAccessTokenMinutes);

    /// <summary>
    /// Refresh-token lifetime in days. "Remember me" grants a longer window
    /// (<c>Jwt:RememberMeRefreshTokenDays</c>); otherwise <c>Jwt:RefreshTokenDays</c> applies.
    /// </summary>
    public static int RefreshTokenDays(IConfiguration configuration, bool rememberMe) =>
        rememberMe
            ? configuration.GetValue("Jwt:RememberMeRefreshTokenDays", DefaultRememberMeRefreshTokenDays)
            : configuration.GetValue("Jwt:RefreshTokenDays", DefaultRefreshTokenDays);
}
