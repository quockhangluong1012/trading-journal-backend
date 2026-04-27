namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Change in State of Delivery (CISD) — a candle whose body closes
/// decisively through a key level (prior swing high/low), indicating a flip
/// in the delivery bias from buying to selling or vice versa.
///
/// Bullish CISD: Candle body closes above a prior swing high (delivery shifts to buying).
/// Bearish CISD: Candle body closes below a prior swing low (delivery shifts to selling).
/// </summary>
internal sealed class CisdDetector : IIctDetector
{
    public IctPatternType PatternType => IctPatternType.CISD;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 15) return patterns;

        // Find swing levels from earlier data (exclude last 3 candles for confirmation)
        var swingHighs = IctHelpers.FindSwingHighs(candles, endIndex: candles.Count - 3);
        var swingLows = IctHelpers.FindSwingLows(candles, endIndex: candles.Count - 3);

        int checkStart = Math.Max(5, candles.Count - 10);

        for (int i = checkStart; i < candles.Count; i++)
        {
            CandleData candle = candles[i];

            // Bullish CISD: body close above a swing high
            foreach (var sh in swingHighs)
            {
                if (sh.Index >= i) continue;

                // Both open AND close must be meaningful relative to the level
                // Close must be above the level, and the candle must have crossed through it
                if (candle.Close > sh.Price &&
                    candle.Open < sh.Price &&
                    IctHelpers.IsBullish(candle))
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.CISD,
                        timeframe,
                        candle.Close,
                        ZoneHigh: candle.Close,
                        ZoneLow: sh.Price,
                        $"Bullish CISD on {symbol} ({timeframe}) — closed through swing high {sh.Price:F5}",
                        candle.Timestamp));
                    break; // One CISD per candle
                }
            }

            // Bearish CISD: body close below a swing low
            foreach (var sl in swingLows)
            {
                if (sl.Index >= i) continue;

                if (candle.Close < sl.Price &&
                    candle.Open > sl.Price &&
                    IctHelpers.IsBearish(candle))
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.CISD,
                        timeframe,
                        candle.Close,
                        ZoneHigh: sl.Price,
                        ZoneLow: candle.Close,
                        $"Bearish CISD on {symbol} ({timeframe}) — closed through swing low {sl.Price:F5}",
                        candle.Timestamp));
                    break;
                }
            }
        }

        return patterns;
    }
}
