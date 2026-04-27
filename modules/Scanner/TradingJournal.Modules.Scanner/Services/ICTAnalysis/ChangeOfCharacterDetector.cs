namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Change of Character (CHoCH) — the first break of the most recent
/// swing high or swing low, signaling an early potential trend reversal.
///
/// Bullish CHoCH: Price breaks above the most recent swing high after making lower lows.
/// Bearish CHoCH: Price breaks below the most recent swing low after making higher highs.
/// </summary>
internal sealed class ChangeOfCharacterDetector : IIctDetector
{
    public IctPatternType PatternType => IctPatternType.ChangeOfCharacter;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 10) return patterns;

        var swingHighs = IctHelpers.FindSwingHighs(candles);
        var swingLows = IctHelpers.FindSwingLows(candles);

        if (swingHighs.Count < 2 || swingLows.Count < 2) return patterns;

        // Check for Bullish CHoCH: downtrend (lower lows), then break of most recent swing high
        var recentSwingHigh = swingHighs[^1];
        var prevSwingLow1 = swingLows.Count >= 2 ? swingLows[^2] : swingLows[^1];
        var prevSwingLow2 = swingLows[^1];

        // Downtrend confirmation: most recent swing low is lower than previous
        if (prevSwingLow2.Price < prevSwingLow1.Price)
        {
            // Check if any candle after the swing high breaks above it
            for (int i = recentSwingHigh.Index + 1; i < candles.Count; i++)
            {
                if (candles[i].Close > recentSwingHigh.Price)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.ChangeOfCharacter,
                        timeframe,
                        candles[i].Close,
                        ZoneHigh: recentSwingHigh.Price,
                        ZoneLow: prevSwingLow2.Price,
                        $"Bullish CHoCH on {symbol} ({timeframe}) — broke swing high {recentSwingHigh.Price:F5}",
                        candles[i].Timestamp));
                    break;
                }
            }
        }

        // Check for Bearish CHoCH: uptrend (higher highs), then break of most recent swing low
        var recentSwingLow = swingLows[^1];
        var prevSwingHigh1 = swingHighs.Count >= 2 ? swingHighs[^2] : swingHighs[^1];
        var prevSwingHigh2 = swingHighs[^1];

        // Uptrend confirmation: most recent swing high is higher than previous
        if (prevSwingHigh2.Price > prevSwingHigh1.Price)
        {
            for (int i = recentSwingLow.Index + 1; i < candles.Count; i++)
            {
                if (candles[i].Close < recentSwingLow.Price)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.ChangeOfCharacter,
                        timeframe,
                        candles[i].Close,
                        ZoneHigh: prevSwingHigh2.Price,
                        ZoneLow: recentSwingLow.Price,
                        $"Bearish CHoCH on {symbol} ({timeframe}) — broke swing low {recentSwingLow.Price:F5}",
                        candles[i].Timestamp));
                    break;
                }
            }
        }

        return patterns;
    }
}
