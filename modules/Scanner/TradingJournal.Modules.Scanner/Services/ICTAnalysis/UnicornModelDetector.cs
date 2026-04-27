namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects the Unicorn Model — a high-probability entry pattern where a
/// Breaker Block zone overlaps with a Fair Value Gap zone.
///
/// The overlap of these two concepts creates a "premium" entry zone.
///
/// Bullish Unicorn: Bullish Breaker Block overlapping with a bullish FVG.
/// Bearish Unicorn: Bearish Breaker Block overlapping with a bearish FVG.
/// </summary>
internal sealed class UnicornModelDetector : IIctDetector
{
    private const int MinImpulseCandles = 3;

    public IctPatternType PatternType => IctPatternType.UnicornModel;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < MinImpulseCandles + 5) return patterns;

        int startIndex = Math.Max(0, candles.Count - 50);

        // Find Breaker Blocks (failed OBs)
        var breakerZones = FindBreakerZones(candles, startIndex);

        // Find FVG zones
        var fvgZones = IctHelpers.FindFvgZones(candles, startIndex);

        // Look for overlaps between breakers and FVGs
        foreach (var breaker in breakerZones)
        {
            foreach (var fvg in fvgZones)
            {
                // Both must be same direction
                if (breaker.IsBullish != fvg.IsBullish) continue;

                // Check for zone overlap
                decimal overlapHigh = Math.Min(breaker.High, fvg.ZoneHigh);
                decimal overlapLow = Math.Max(breaker.Low, fvg.ZoneLow);

                if (overlapHigh > overlapLow)
                {
                    string direction = breaker.IsBullish ? "Bullish" : "Bearish";

                    patterns.Add(new DetectedPattern(
                        IctPatternType.UnicornModel,
                        timeframe,
                        candles[Math.Max(breaker.DetectionIndex, fvg.Index)].Close,
                        ZoneHigh: overlapHigh,
                        ZoneLow: overlapLow,
                        $"{direction} Unicorn Model on {symbol} ({timeframe}) — Breaker+FVG overlap at {overlapLow:F5}-{overlapHigh:F5}",
                        candles[Math.Max(breaker.DetectionIndex, fvg.Index)].Timestamp));
                }
            }
        }

        return patterns;
    }

    /// <summary>
    /// Finds Breaker Block zones — failed Order Blocks.
    /// </summary>
    private static List<(decimal High, decimal Low, bool IsBullish, int DetectionIndex)> FindBreakerZones(
        IReadOnlyList<CandleData> candles, int startIndex)
    {
        var zones = new List<(decimal, decimal, bool, int)>();
        var obZones = IctHelpers.FindOrderBlockZones(candles, 3, startIndex);

        foreach (var (obHigh, obLow, isBullish, obIndex, impulseEnd) in obZones)
        {
            for (int k = impulseEnd + 1; k < candles.Count; k++)
            {
                if (isBullish && candles[k].Close < obLow)
                {
                    // Bullish OB failed → becomes Bearish Breaker... 
                    // but for Unicorn we want Bullish Breaker (bearish OB that failed)
                    zones.Add((obHigh, obLow, false, k));
                    break;
                }

                if (!isBullish && candles[k].Close > obHigh)
                {
                    // Bearish OB failed → becomes Bullish Breaker
                    zones.Add((obHigh, obLow, true, k));
                    break;
                }
            }
        }

        return zones;
    }
}
