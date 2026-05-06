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
    public const string TradingZones = "shared:zones";

    /// <summary>
    /// Returns a user-scoped cache key for trades: "shared:trades:{userId}"
    /// </summary>
    public static string TradesForUser(int userId) => $"shared:trades:{userId}";

    /// <summary>
    /// Returns a user-scoped cache key for discipline rules: "shared:discipline-rules:{userId}"
    /// </summary>
    public static string DisciplineRulesForUser(int userId) => $"shared:discipline-rules:{userId}";

    /// <summary>
    /// Returns a user-scoped cache key for trading setups: "shared:setups:{userId}"
    /// </summary>
    public static string SetupsForUser(int userId) => $"shared:setups:{userId}";

    /// <summary>
    /// Returns a user-scoped cache key for risk config: "shared:risk-config:{userId}"
    /// </summary>
    public static string RiskConfigForUser(int userId) => $"shared:risk-config:{userId}";

    /// <summary>
    /// Returns a user-scoped cache key for karma summary: "shared:karma-summary:{userId}"
    /// </summary>
    public static string KarmaSummaryForUser(int userId) => $"shared:karma-summary:{userId}";

    /// <summary>
    /// Returns a user-scoped cache key for achievements: "shared:achievements:{userId}"
    /// </summary>
    public static string AchievementsForUser(int userId) => $"shared:achievements:{userId}";

    /// <summary>
    /// Returns a user-scoped cache key for notification unread count: "shared:unread-count:{userId}"
    /// </summary>
    public static string UnreadCountForUser(int userId) => $"shared:unread-count:{userId}";
}
