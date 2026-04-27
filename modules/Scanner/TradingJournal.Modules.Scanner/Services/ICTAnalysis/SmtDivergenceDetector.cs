namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Smart Money Technique (SMT) Divergence — when two correlated assets
/// diverge at key levels, indicating institutional activity.
///
/// Example: ES makes a new high while NQ fails to make a new high → bearish SMT divergence.
///
/// Bullish SMT: Primary asset makes a new low, correlated asset holds higher → reversal up.
/// Bearish SMT: Primary asset makes a new high, correlated asset holds lower → reversal down.
/// </summary>
internal sealed class SmtDivergenceDetector : IMultiAssetDetector
{
    /// <summary>
    /// Number of recent swing points to compare for divergence.
    /// </summary>
    private const int SwingLookback = 3;

    public IctPatternType PatternType => IctPatternType.SMTDivergence;

    /// <summary>
    /// Default correlation pairs. Keys map to their correlated counterpart.
    /// </summary>
    public static readonly Dictionary<string, string> CorrelationPairs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ES"] = "NQ",
        ["NQ"] = "ES",
        ["EURUSD"] = "DXY",
        ["DXY"] = "EURUSD",
        ["GBPUSD"] = "DXY",
        ["XAUUSD"] = "DXY",
        ["GOLD"] = "DXY"
    };

    /// <summary>
    /// Gets the correlated symbol for a given primary symbol.
    /// Returns null if no known correlation exists.
    /// </summary>
    public static string? GetCorrelatedSymbol(string symbol)
    {
        return CorrelationPairs.TryGetValue(symbol, out string? correlated) ? correlated : null;
    }

    public List<DetectedPattern> Detect(
        string primarySymbol,
        IReadOnlyList<CandleData> primaryCandles,
        string correlatedSymbol,
        IReadOnlyList<CandleData> correlatedCandles,
        ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (primaryCandles.Count < 10 || correlatedCandles.Count < 10) return patterns;

        var primaryHighs = IctHelpers.FindSwingHighs(primaryCandles);
        var primaryLows = IctHelpers.FindSwingLows(primaryCandles);
        var correlatedHighs = IctHelpers.FindSwingHighs(correlatedCandles);
        var correlatedLows = IctHelpers.FindSwingLows(correlatedCandles);

        if (primaryHighs.Count < 2 || correlatedHighs.Count < 2 ||
            primaryLows.Count < 2 || correlatedLows.Count < 2)
            return patterns;

        // Bearish SMT: Primary makes new high, correlated fails
        int highCheckCount = Math.Min(SwingLookback, Math.Min(primaryHighs.Count, correlatedHighs.Count));
        for (int i = 1; i < highCheckCount; i++)
        {
            var currPrimary = primaryHighs[^i];
            var prevPrimary = primaryHighs[^(i + 1)];

            // Primary made a higher high
            if (currPrimary.Price > prevPrimary.Price)
            {
                // Find correlated high around the same time
                var currCorr = correlatedHighs[^i];
                var prevCorr = correlatedHighs.Count > i ? correlatedHighs[^(i + 1)] : correlatedHighs[^i];

                // Correlated failed to make a higher high → divergence
                if (currCorr.Price <= prevCorr.Price)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.SMTDivergence,
                        timeframe,
                        primaryCandles[^1].Close,
                        ZoneHigh: currPrimary.Price,
                        ZoneLow: prevPrimary.Price,
                        $"Bearish SMT Divergence on {primarySymbol} vs {correlatedSymbol} ({timeframe}) — {primarySymbol} new high, {correlatedSymbol} failed",
                        currPrimary.Timestamp));
                    break;
                }
            }
        }

        // Bullish SMT: Primary makes new low, correlated holds higher
        int lowCheckCount = Math.Min(SwingLookback, Math.Min(primaryLows.Count, correlatedLows.Count));
        for (int i = 1; i < lowCheckCount; i++)
        {
            var currPrimary = primaryLows[^i];
            var prevPrimary = primaryLows[^(i + 1)];

            // Primary made a lower low
            if (currPrimary.Price < prevPrimary.Price)
            {
                var currCorr = correlatedLows[^i];
                var prevCorr = correlatedLows.Count > i ? correlatedLows[^(i + 1)] : correlatedLows[^i];

                // Correlated held a higher low → divergence
                if (currCorr.Price >= prevCorr.Price)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.SMTDivergence,
                        timeframe,
                        primaryCandles[^1].Close,
                        ZoneHigh: prevPrimary.Price,
                        ZoneLow: currPrimary.Price,
                        $"Bullish SMT Divergence on {primarySymbol} vs {correlatedSymbol} ({timeframe}) — {primarySymbol} new low, {correlatedSymbol} held",
                        currPrimary.Timestamp));
                    break;
                }
            }
        }

        return patterns;
    }
}
