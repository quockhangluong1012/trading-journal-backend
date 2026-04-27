namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Judas Swing — a false breakout during ICT kill-zone session opens
/// (London or New York) that reverses and traps traders on the wrong side.
///
/// The detector:
/// 1. Identifies the prior session's range (using last 12 candles as proxy)
/// 2. During kill-zone hours, looks for a breakout beyond the range
/// 3. Confirms a reversal candle that closes back inside the range
///
/// Kill zones (UTC):
/// - London: 02:00–05:00 (07:00–10:00 UTC on some definitions)
/// - New York: 12:00–15:00
/// </summary>
internal sealed class JudasSwingDetector : IIctDetector
{
    public IctPatternType PatternType => IctPatternType.JudasSwing;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 20) return patterns;

        // Calculate prior range from candles before the recent ones
        int rangeEnd = Math.Max(10, candles.Count - 10);
        int rangeStart = Math.Max(0, rangeEnd - 12);

        decimal rangeHigh = decimal.MinValue;
        decimal rangeLow = decimal.MaxValue;

        for (int i = rangeStart; i < rangeEnd; i++)
        {
            if (candles[i].High > rangeHigh) rangeHigh = candles[i].High;
            if (candles[i].Low < rangeLow) rangeLow = candles[i].Low;
        }

        if (rangeHigh <= rangeLow) return patterns;

        // Check recent candles for Judas Swing pattern
        int checkStart = Math.Max(rangeEnd, candles.Count - 10);

        for (int i = checkStart; i < candles.Count; i++)
        {
            CandleData candle = candles[i];

            // Check if this candle occurs during a kill zone
            if (!IsKillZone(candle.Timestamp)) continue;

            // Bearish Judas Swing: wick breaks above range high, but closes back below
            if (candle.High > rangeHigh && candle.Close < rangeHigh && candle.Close < candle.Open)
            {
                patterns.Add(new DetectedPattern(
                    IctPatternType.JudasSwing,
                    timeframe,
                    candle.Close,
                    ZoneHigh: candle.High,
                    ZoneLow: rangeHigh,
                    $"Bearish Judas Swing on {symbol} ({timeframe}) — false break above {rangeHigh:F5} during kill zone",
                    candle.Timestamp));
            }

            // Bullish Judas Swing: wick breaks below range low, but closes back above
            if (candle.Low < rangeLow && candle.Close > rangeLow && candle.Close > candle.Open)
            {
                patterns.Add(new DetectedPattern(
                    IctPatternType.JudasSwing,
                    timeframe,
                    candle.Close,
                    ZoneHigh: rangeLow,
                    ZoneLow: candle.Low,
                    $"Bullish Judas Swing on {symbol} ({timeframe}) — false break below {rangeLow:F5} during kill zone",
                    candle.Timestamp));
            }
        }

        return patterns;
    }

    /// <summary>
    /// Checks if the given timestamp falls within an ICT kill zone.
    /// London: 07:00–10:00 UTC  |  New York: 12:00–15:00 UTC
    /// </summary>
    private static bool IsKillZone(DateTime timestamp)
    {
        int hour = timestamp.Hour;

        // London kill zone
        if (hour >= 7 && hour < 10) return true;

        // New York kill zone
        if (hour >= 12 && hour < 15) return true;

        return false;
    }
}
