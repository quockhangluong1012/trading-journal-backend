namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Mitigation Block — an Order Block that was never mitigated (price never
/// returned to it), and then price finally returns to the zone and reacts from it.
///
/// Bullish Mitigation: Unmitigated bullish OB that price returns to and bounces from.
/// Bearish Mitigation: Unmitigated bearish OB that price returns to and gets rejected.
/// </summary>
internal sealed class MitigationBlockDetector : IIctDetector
{
    private const int MinImpulseCandles = 3;

    /// <summary>
    /// Minimum candles between OB formation and return for it to be "unmitigated".
    /// </summary>
    private const int MinCandlesBeforeReturn = 5;

    public IctPatternType PatternType => IctPatternType.MitigationBlock;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < MinImpulseCandles + MinCandlesBeforeReturn + 3) return patterns;

        int startIndex = Math.Max(0, candles.Count - 50);
        var obZones = IctHelpers.FindOrderBlockZones(candles, MinImpulseCandles, startIndex);

        foreach (var (obHigh, obLow, isBullish, obIndex, impulseEnd) in obZones)
        {
            // Check that the OB was unmitigated for at least MinCandlesBeforeReturn
            bool wasMitigated = false;
            int unmitigatedUntil = Math.Min(impulseEnd + MinCandlesBeforeReturn, candles.Count);

            for (int i = impulseEnd + 1; i < unmitigatedUntil && i < candles.Count; i++)
            {
                if (isBullish && candles[i].Low <= obHigh)
                {
                    wasMitigated = true;
                    break;
                }

                if (!isBullish && candles[i].High >= obLow)
                {
                    wasMitigated = true;
                    break;
                }
            }

            if (wasMitigated) continue;

            // Now check if price returns to the zone later and reacts
            for (int i = unmitigatedUntil; i < candles.Count; i++)
            {
                if (isBullish)
                {
                    // Price returns down to the bullish OB zone and bounces
                    if (candles[i].Low <= obHigh && candles[i].Low >= obLow &&
                        candles[i].Close > obLow && IctHelpers.IsBullish(candles[i]))
                    {
                        patterns.Add(new DetectedPattern(
                            IctPatternType.MitigationBlock,
                            timeframe,
                            candles[i].Close,
                            ZoneHigh: obHigh,
                            ZoneLow: obLow,
                            $"Bullish Mitigation Block on {symbol} ({timeframe}) — unmitigated OB at {obLow:F5}-{obHigh:F5}",
                            candles[i].Timestamp));
                        break;
                    }

                    // OB failed — price closed below
                    if (candles[i].Close < obLow) break;
                }
                else
                {
                    // Price returns up to the bearish OB zone and gets rejected
                    if (candles[i].High >= obLow && candles[i].High <= obHigh &&
                        candles[i].Close < obHigh && IctHelpers.IsBearish(candles[i]))
                    {
                        patterns.Add(new DetectedPattern(
                            IctPatternType.MitigationBlock,
                            timeframe,
                            candles[i].Close,
                            ZoneHigh: obHigh,
                            ZoneLow: obLow,
                            $"Bearish Mitigation Block on {symbol} ({timeframe}) — unmitigated OB at {obLow:F5}-{obHigh:F5}",
                            candles[i].Timestamp));
                        break;
                    }

                    if (candles[i].Close > obHigh) break;
                }
            }
        }

        return patterns;
    }
}
