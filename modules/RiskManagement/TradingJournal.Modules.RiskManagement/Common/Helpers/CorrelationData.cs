namespace TradingJournal.Modules.RiskManagement.Common.Helpers;

/// <summary>
/// Hardcoded forex correlation matrix based on standard industry data.
/// Values range from -1 (inverse) to +1 (perfect correlation).
/// Positive > 0.7 = strongly correlated, negative < -0.7 = strongly inverse.
/// </summary>
public static class CorrelationData
{
    private static readonly Dictionary<string, Dictionary<string, decimal>> Correlations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EURUSD"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GBPUSD"] = 0.85m, ["AUDUSD"] = 0.68m, ["NZDUSD"] = 0.62m,
            ["USDCHF"] = -0.92m, ["USDJPY"] = -0.55m, ["USDCAD"] = -0.70m,
            ["EURGBP"] = 0.30m, ["EURJPY"] = 0.60m, ["GBPJPY"] = 0.50m,
            ["XAUUSD"] = 0.40m,
        },
        ["GBPUSD"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = 0.85m, ["AUDUSD"] = 0.60m, ["NZDUSD"] = 0.55m,
            ["USDCHF"] = -0.80m, ["USDJPY"] = -0.50m, ["USDCAD"] = -0.62m,
            ["EURGBP"] = -0.45m, ["EURJPY"] = 0.48m, ["GBPJPY"] = 0.72m,
            ["XAUUSD"] = 0.35m,
        },
        ["USDJPY"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = -0.55m, ["GBPUSD"] = -0.50m, ["AUDUSD"] = -0.42m,
            ["NZDUSD"] = -0.38m, ["USDCHF"] = 0.55m, ["USDCAD"] = 0.45m,
            ["EURJPY"] = 0.60m, ["GBPJPY"] = 0.65m, ["XAUUSD"] = -0.60m,
        },
        ["USDCHF"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = -0.92m, ["GBPUSD"] = -0.80m, ["AUDUSD"] = -0.60m,
            ["NZDUSD"] = -0.55m, ["USDJPY"] = 0.55m, ["USDCAD"] = 0.65m,
            ["XAUUSD"] = -0.45m,
        },
        ["AUDUSD"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = 0.68m, ["GBPUSD"] = 0.60m, ["NZDUSD"] = 0.88m,
            ["USDCHF"] = -0.60m, ["USDJPY"] = -0.42m, ["USDCAD"] = -0.58m,
            ["XAUUSD"] = 0.65m,
        },
        ["NZDUSD"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = 0.62m, ["GBPUSD"] = 0.55m, ["AUDUSD"] = 0.88m,
            ["USDCHF"] = -0.55m, ["USDJPY"] = -0.38m, ["USDCAD"] = -0.52m,
            ["XAUUSD"] = 0.55m,
        },
        ["USDCAD"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = -0.70m, ["GBPUSD"] = -0.62m, ["AUDUSD"] = -0.58m,
            ["NZDUSD"] = -0.52m, ["USDCHF"] = 0.65m, ["USDJPY"] = 0.45m,
            ["XAUUSD"] = -0.40m,
        },
        ["XAUUSD"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = 0.40m, ["GBPUSD"] = 0.35m, ["AUDUSD"] = 0.65m,
            ["NZDUSD"] = 0.55m, ["USDCHF"] = -0.45m, ["USDJPY"] = -0.60m,
            ["USDCAD"] = -0.40m,
        },
    };

    /// <summary>
    /// Returns the correlation coefficient between two assets.
    /// Returns 1.0 for same asset, 0.0 if no data is available.
    /// </summary>
    public static decimal GetCorrelation(string asset1, string asset2)
    {
        if (string.Equals(asset1, asset2, StringComparison.OrdinalIgnoreCase))
            return 1.0m;

        if (Correlations.TryGetValue(asset1, out var inner) && inner.TryGetValue(asset2, out var correlation))
            return correlation;

        if (Correlations.TryGetValue(asset2, out var reverseInner) && reverseInner.TryGetValue(asset1, out var reverseCorrelation))
            return reverseCorrelation;

        return 0.0m;
    }

    /// <summary>
    /// Returns all known assets in the correlation matrix.
    /// </summary>
    public static IReadOnlyList<string> KnownAssets => [.. Correlations.Keys];
}
