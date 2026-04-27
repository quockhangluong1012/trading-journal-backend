namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Inversion Fair Value Gap (iFVG) — an FVG that price trades through,
/// causing the gap zone to "invert" and act as support/resistance from the
/// opposite side on a subsequent revisit.
///
/// Bullish iFVG: A bearish FVG gets traded through to the upside, then
///               acts as support when price returns.
/// Bearish iFVG: A bullish FVG gets traded through to the downside, then
///               acts as resistance when price returns.
/// </summary>
internal sealed class InversionFvgDetector : IIctDetector
{
    public IctPatternType PatternType => IctPatternType.InversionFVG;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 10) return patterns;

        int startIndex = Math.Max(0, candles.Count - 50);
        var fvgZones = IctHelpers.FindFvgZones(candles, startIndex);

        foreach (var (zoneHigh, zoneLow, isBullish, fvgIndex) in fvgZones)
        {
            bool traded_through = false;
            int tradeThruIndex = -1;

            // Step 1: Check if price traded through the FVG
            for (int i = fvgIndex + 2; i < candles.Count; i++)
            {
                if (isBullish && candles[i].Close < zoneLow)
                {
                    // Bullish FVG traded through to downside → becomes bearish iFVG
                    traded_through = true;
                    tradeThruIndex = i;
                    break;
                }

                if (!isBullish && candles[i].Close > zoneHigh)
                {
                    // Bearish FVG traded through to upside → becomes bullish iFVG
                    traded_through = true;
                    tradeThruIndex = i;
                    break;
                }
            }

            if (!traded_through || tradeThruIndex < 0) continue;

            // Step 2: Check if price returns to the inverted zone and reacts
            for (int i = tradeThruIndex + 1; i < candles.Count; i++)
            {
                if (isBullish)
                {
                    // Originally bullish FVG → now bearish iFVG (resistance)
                    // Price comes back up to the zone and gets rejected
                    if (candles[i].High >= zoneLow && candles[i].Close < zoneHigh &&
                        IctHelpers.IsBearish(candles[i]))
                    {
                        patterns.Add(new DetectedPattern(
                            IctPatternType.InversionFVG,
                            timeframe,
                            candles[i].Close,
                            ZoneHigh: zoneHigh,
                            ZoneLow: zoneLow,
                            $"Bearish iFVG on {symbol} ({timeframe}) — inverted FVG resistance at {zoneLow:F5}-{zoneHigh:F5}",
                            candles[i].Timestamp));
                        break;
                    }
                }
                else
                {
                    // Originally bearish FVG → now bullish iFVG (support)
                    // Price comes back down to the zone and bounces
                    if (candles[i].Low <= zoneHigh && candles[i].Close > zoneLow &&
                        IctHelpers.IsBullish(candles[i]))
                    {
                        patterns.Add(new DetectedPattern(
                            IctPatternType.InversionFVG,
                            timeframe,
                            candles[i].Close,
                            ZoneHigh: zoneHigh,
                            ZoneLow: zoneLow,
                            $"Bullish iFVG on {symbol} ({timeframe}) — inverted FVG support at {zoneLow:F5}-{zoneHigh:F5}",
                            candles[i].Timestamp));
                        break;
                    }
                }
            }
        }

        return patterns;
    }
}
