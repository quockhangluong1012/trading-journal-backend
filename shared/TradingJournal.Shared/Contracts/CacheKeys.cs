namespace TradingJournal.Shared.Contracts;

/// <summary>
/// Shared cache key constants for cross-module data.
/// Keys prefixed with "shared:" to distinguish from module-local cache entries.
/// User-scoped keys include the userId to prevent cross-user data leakage.
/// </summary>
public static class CacheKeys
{
    public const string EmotionTags = "shared:emotions";
    public const string Reviews = "shared:reviews";

    /// <summary>
    /// Returns a user-scoped cache key for trades: "shared:trades:{userId}"
    /// </summary>
    public static string TradesForUser(int userId) => $"shared:trades:{userId}";
}
