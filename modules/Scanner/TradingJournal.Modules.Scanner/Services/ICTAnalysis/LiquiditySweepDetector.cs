namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Liquidity Sweeps (stop hunts) — price briefly exceeds a liquidity
/// level (swing high/low) then reverses, indicated by a wick beyond the level
/// followed by a close back inside.
///
/// Bullish sweep: wick below a swing low, then close back above it.
/// Bearish sweep: wick above a swing high, then close back below it.
/// </summary>
internal sealed class LiquiditySweepDetector : IIctDetector
{
    private const decimal TolerancePercent = 0.0005m;

    public IctPatternType PatternType => IctPatternType.LiquiditySweep;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 10) return patterns;

        // Find swing levels from earlier data
        List<decimal> swingHighs = FindSwingHighPrices(candles, endBefore: candles.Count - 3);
        List<decimal> swingLows = FindSwingLowPrices(candles, endBefore: candles.Count - 3);

        // Check the most recent candles for sweeps
        int checkStart = Math.Max(5, candles.Count - 10);

        for (int i = checkStart; i < candles.Count; i++)
        {
            CandleData candle = candles[i];

            // Check for bearish sweep (wick above swing high, close below)
            foreach (decimal swingHigh in swingHighs)
            {
                decimal tolerance = swingHigh * TolerancePercent;

                // Wick exceeds swing high but body closes below
                if (candle.High > swingHigh + tolerance &&
                    candle.Close < swingHigh &&
                    candle.Open < swingHigh)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.LiquiditySweep,
                        timeframe,
                        candle.Close,
                        ZoneHigh: candle.High,
                        ZoneLow: swingHigh,
                        $"Bearish liquidity sweep on {symbol} ({timeframe}) — swept high {swingHigh:F5}, wick to {candle.High:F5}",
                        candle.Timestamp));
                }
            }

            // Check for bullish sweep (wick below swing low, close above)
            foreach (decimal swingLow in swingLows)
            {
                decimal tolerance = swingLow * TolerancePercent;

                if (candle.Low < swingLow - tolerance &&
                    candle.Close > swingLow &&
                    candle.Open > swingLow)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.LiquiditySweep,
                        timeframe,
                        candle.Close,
                        ZoneHigh: swingLow,
                        ZoneLow: candle.Low,
                        $"Bullish liquidity sweep on {symbol} ({timeframe}) — swept low {swingLow:F5}, wick to {candle.Low:F5}",
                        candle.Timestamp));
                }
            }
        }

        return patterns;
    }

    private static List<decimal> FindSwingHighPrices(IReadOnlyList<CandleData> candles, int endBefore)
    {
        var highs = new List<decimal>();

        for (int i = 2; i < Math.Min(candles.Count - 2, endBefore); i++)
        {
            if (candles[i].High > candles[i - 1].High &&
                candles[i].High > candles[i - 2].High &&
                candles[i].High > candles[i + 1].High &&
                candles[i].High > candles[i + 2].High)
            {
                highs.Add(candles[i].High);
            }
        }

        return highs;
    }

    private static List<decimal> FindSwingLowPrices(IReadOnlyList<CandleData> candles, int endBefore)
    {
        var lows = new List<decimal>();

        for (int i = 2; i < Math.Min(candles.Count - 2, endBefore); i++)
        {
            if (candles[i].Low < candles[i - 1].Low &&
                candles[i].Low < candles[i - 2].Low &&
                candles[i].Low < candles[i + 1].Low &&
                candles[i].Low < candles[i + 2].Low)
            {
                lows.Add(candles[i].Low);
            }
        }

        return lows;
    }
}
