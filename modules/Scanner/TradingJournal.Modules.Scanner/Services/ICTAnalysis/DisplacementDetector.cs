namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Displacement — large impulsive candle(s) where the body covers
/// ≥ 80% of the total range AND the range is ≥ 1.5× the ATR(14).
///
/// Bullish Displacement: Large bullish candle(s) with full body, minimal wicks.
/// Bearish Displacement: Large bearish candle(s) with full body, minimal wicks.
/// </summary>
internal sealed class DisplacementDetector : IIctDetector
{
    /// <summary>
    /// Minimum body-to-range ratio for a displacement candle.
    /// </summary>
    private const decimal MinBodyRangeRatio = 0.80m;

    /// <summary>
    /// Minimum ATR multiplier for the candle range to qualify as displacement.
    /// </summary>
    private const decimal MinAtrMultiplier = 1.5m;

    public IctPatternType PatternType => IctPatternType.Displacement;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 15) return patterns;

        decimal atr = IctHelpers.CalculateAtr(candles);
        if (atr <= 0) return patterns;

        int startIndex = Math.Max(15, candles.Count - 30);

        for (int i = startIndex; i < candles.Count; i++)
        {
            CandleData candle = candles[i];
            decimal body = IctHelpers.BodySize(candle);
            decimal range = IctHelpers.Range(candle);

            if (range <= 0) continue;

            decimal bodyRatio = body / range;
            decimal atrMultiple = range / atr;

            if (bodyRatio >= MinBodyRangeRatio && atrMultiple >= MinAtrMultiplier)
            {
                string direction = IctHelpers.IsBullish(candle) ? "Bullish" : "Bearish";

                patterns.Add(new DetectedPattern(
                    IctPatternType.Displacement,
                    timeframe,
                    candle.Close,
                    ZoneHigh: candle.High,
                    ZoneLow: candle.Low,
                    $"{direction} Displacement on {symbol} ({timeframe}) — {atrMultiple:F1}× ATR, {bodyRatio:P0} body",
                    candle.Timestamp));
            }
        }

        return patterns;
    }
}
