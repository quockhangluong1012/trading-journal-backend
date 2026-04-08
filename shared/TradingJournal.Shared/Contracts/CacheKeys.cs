namespace TradingJournal.Shared.Contracts;

/// <summary>
/// Shared cache key constants for cross-module data.
/// Keys prefixed with "shared:" to distinguish from module-local cache entries.
/// </summary>
public static class CacheKeys
{
    public const string EmotionTags = "shared:emotions";
    public const string Trades = "shared:trades";
    public const string Reviews = "shared:reviews";
}
