namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Optimal Trade Entry (OTE) — price retracing into the 62%-79%
/// Fibonacci zone of a prior impulse leg (displacement).
///
/// Bullish OTE: After a bullish impulse, price retraces down into the 62-79% zone.
/// Bearish OTE: After a bearish impulse, price retraces up into the 62-79% zone.
/// </summary>
internal sealed class OptimalTradeEntryDetector : IIctDetector
{
    private const decimal FibUpper = 0.79m; // 79% retracement
    private const decimal FibLower = 0.62m; // 62% retracement
    private const decimal MinAtrMultiplier = 1.5m;

    public IctPatternType PatternType => IctPatternType.OptimalTradeEntry;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 20) return patterns;

        decimal atr = IctHelpers.CalculateAtr(candles);
        if (atr <= 0) return patterns;

        var swingHighs = IctHelpers.FindSwingHighs(candles);
        var swingLows = IctHelpers.FindSwingLows(candles);

        // Look for bullish OTE: find impulse leg (swing low → swing high), then retracement
        for (int h = 0; h < swingHighs.Count; h++)
        {
            // Find the closest preceding swing low
            var precedingLows = swingLows.Where(l => l.Index < swingHighs[h].Index).ToList();
            if (precedingLows.Count == 0) continue;

            var swingLow = precedingLows[^1];
            decimal impulseLeg = swingHighs[h].Price - swingLow.Price;

            // Impulse must be significant
            if (impulseLeg < atr * MinAtrMultiplier) continue;

            // Calculate OTE zone
            decimal oteHigh = swingHighs[h].Price - impulseLeg * FibLower; // 62% level
            decimal oteLow = swingHighs[h].Price - impulseLeg * FibUpper;  // 79% level

            // Check if price retraces into OTE zone after the swing high
            for (int i = swingHighs[h].Index + 1; i < candles.Count; i++)
            {
                if (candles[i].Low <= oteHigh && candles[i].Low >= oteLow)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.OptimalTradeEntry,
                        timeframe,
                        candles[i].Close,
                        ZoneHigh: oteHigh,
                        ZoneLow: oteLow,
                        $"Bullish OTE on {symbol} ({timeframe}) — retraced to 62-79% fib zone {oteLow:F5}-{oteHigh:F5}",
                        candles[i].Timestamp));
                    break;
                }

                // If price breaks below OTE zone entirely, impulse invalidated
                if (candles[i].Close < oteLow) break;
            }
        }

        // Look for bearish OTE: find impulse leg (swing high → swing low), then retracement
        for (int l = 0; l < swingLows.Count; l++)
        {
            var precedingHighs = swingHighs.Where(h => h.Index < swingLows[l].Index).ToList();
            if (precedingHighs.Count == 0) continue;

            var swingHigh = precedingHighs[^1];
            decimal impulseLeg = swingHigh.Price - swingLows[l].Price;

            if (impulseLeg < atr * MinAtrMultiplier) continue;

            decimal oteLow = swingLows[l].Price + impulseLeg * FibLower;  // 62% level
            decimal oteHigh = swingLows[l].Price + impulseLeg * FibUpper; // 79% level

            for (int i = swingLows[l].Index + 1; i < candles.Count; i++)
            {
                if (candles[i].High >= oteLow && candles[i].High <= oteHigh)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.OptimalTradeEntry,
                        timeframe,
                        candles[i].Close,
                        ZoneHigh: oteHigh,
                        ZoneLow: oteLow,
                        $"Bearish OTE on {symbol} ({timeframe}) — retraced to 62-79% fib zone {oteLow:F5}-{oteHigh:F5}",
                        candles[i].Timestamp));
                    break;
                }

                if (candles[i].Close > oteHigh) break;
            }
        }

        return patterns;
    }
}
