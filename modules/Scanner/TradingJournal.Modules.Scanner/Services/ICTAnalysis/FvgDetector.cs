namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Fair Value Gaps (FVG) — 3-candle patterns where a gap exists
/// between candle[0] and candle[2] that candle[1] doesn't fill.
///
/// Bullish FVG: candle[2].Low > candle[0].High (gap up)
/// Bearish FVG: candle[2].High < candle[0].Low (gap down)
/// </summary>
internal sealed class FvgDetector : IIctDetector
{
    public IctPatternType PatternType => IctPatternType.FVG;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 3) return patterns;

        // Only check the most recent candles (last 50) to avoid stale alerts
        int startIndex = Math.Max(0, candles.Count - 50);

        for (int i = startIndex; i <= candles.Count - 3; i++)
        {
            CandleData candle0 = candles[i];
            CandleData candle1 = candles[i + 1];
            CandleData candle2 = candles[i + 2];

            // Bullish FVG: gap between candle[0].High and candle[2].Low
            if (candle2.Low > candle0.High)
            {
                decimal gapSize = candle2.Low - candle0.High;
                decimal bodySize = Math.Abs(candle1.Close - candle1.Open);

                // Only report significant gaps (gap > 20% of middle candle body)
                if (bodySize > 0 && gapSize / bodySize > 0.2m)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.FVG,
                        timeframe,
                        candle2.Close,
                        ZoneHigh: candle2.Low,
                        ZoneLow: candle0.High,
                        $"Bullish FVG on {symbol} ({timeframe}) — gap {candle0.High:F5} to {candle2.Low:F5}",
                        candle2.Timestamp));
                }
            }

            // Bearish FVG: gap between candle[0].Low and candle[2].High
            if (candle2.High < candle0.Low)
            {
                decimal gapSize = candle0.Low - candle2.High;
                decimal bodySize = Math.Abs(candle1.Close - candle1.Open);

                if (bodySize > 0 && gapSize / bodySize > 0.2m)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.FVG,
                        timeframe,
                        candle2.Close,
                        ZoneHigh: candle0.Low,
                        ZoneLow: candle2.High,
                        $"Bearish FVG on {symbol} ({timeframe}) — gap {candle2.High:F5} to {candle0.Low:F5}",
                        candle2.Timestamp));
                }
            }
        }

        return patterns;
    }
}
