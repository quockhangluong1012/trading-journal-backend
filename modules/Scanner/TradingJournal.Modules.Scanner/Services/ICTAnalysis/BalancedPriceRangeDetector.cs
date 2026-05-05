namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Balanced Price Range (BPR) — the overlapping area between a
/// bullish FVG and a bearish FVG that form in close proximity.
/// This overlap zone represents "balanced" price delivery.
/// </summary>
internal sealed class BalancedPriceRangeDetector : IIctDetector
{
    /// <summary>
    /// Maximum number of candles between the two FVGs to be considered "close proximity".
    /// </summary>
    private const int MaxGapBetweenFvgs = 10;

    public IctPatternType PatternType => IctPatternType.BalancedPriceRange;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 10) return patterns;

        int startIndex = Math.Max(0, candles.Count - 50);
        var fvgZones = IctHelpers.FindFvgZones(candles, startIndex);

        var bullishFvgs = fvgZones.Where(z => z.IsBullish).ToList();
        var bearishFvgs = fvgZones.Where(z => !z.IsBullish).ToList();

        foreach (var bull in bullishFvgs)
        {
            foreach (var bear in bearishFvgs)
            {
                // Must be in close proximity
                if (Math.Abs(bull.Index - bear.Index) > MaxGapBetweenFvgs) continue;

                // Check for overlap
                decimal overlapHigh = Math.Min(bull.ZoneHigh, bear.ZoneHigh);
                decimal overlapLow = Math.Max(bull.ZoneLow, bear.ZoneLow);

                if (overlapHigh > overlapLow)
                {
                    int latestIndex = Math.Max(bull.Index, bear.Index);
                    DateTime detectedAt = candles[Math.Min(latestIndex + 1, candles.Count - 1)].Timestamp;

                    patterns.Add(new DetectedPattern(
                        IctPatternType.BalancedPriceRange,
                        timeframe,
                        candles[latestIndex].Close,
                        ZoneHigh: overlapHigh,
                        ZoneLow: overlapLow,
                        $"Balanced Price Range on {symbol} ({timeframe}) — overlap zone {overlapLow:F5}-{overlapHigh:F5}",
                        detectedAt));
                }
            }
        }

        return patterns;
    }
}
