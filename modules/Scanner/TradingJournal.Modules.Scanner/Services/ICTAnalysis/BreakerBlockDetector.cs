namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Breaker Blocks — failed Order Blocks where price returns through
/// the OB zone and breaks structure in the opposite direction.
///
/// A Breaker forms when:
/// 1. An Order Block is identified
/// 2. Price returns to and breaks through the OB zone
/// 3. The OB "fails" and becomes a Breaker Block in the new direction
/// </summary>
internal sealed class BreakerBlockDetector : IIctDetector
{
    private const int MinImpulseCandles = 3;
    private const int LookbackWindow = 30;

    public IctPatternType PatternType => IctPatternType.BreakerBlock;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < MinImpulseCandles + 5) return patterns;

        int startIndex = Math.Max(0, candles.Count - LookbackWindow);

        for (int i = startIndex; i < candles.Count - MinImpulseCandles - 2; i++)
        {
            CandleData candidate = candles[i];
            bool isCandidateBearish = candidate.Close < candidate.Open;
            bool isCandidateBullish = candidate.Close > candidate.Open;

            // Look for a Bullish OB that gets broken (becomes Bearish Breaker)
            if (isCandidateBearish)
            {
                int bullishCount = 0;
                for (int j = i + 1; j < candles.Count && candles[j].Close > candles[j].Open; j++)
                {
                    bullishCount++;
                }

                if (bullishCount >= MinImpulseCandles)
                {
                    decimal obLow = Math.Min(candidate.Open, candidate.Close);

                    // Check if price later breaks below the OB low (OB fails → Breaker)
                    int afterImpulse = i + 1 + bullishCount;
                    for (int k = afterImpulse; k < candles.Count; k++)
                    {
                        if (candles[k].Close < obLow)
                        {
                            decimal obHigh = Math.Max(candidate.Open, candidate.Close);
                            patterns.Add(new DetectedPattern(
                                IctPatternType.BreakerBlock,
                                timeframe,
                                candles[k].Close,
                                ZoneHigh: obHigh,
                                ZoneLow: obLow,
                                $"Bearish Breaker Block on {symbol} ({timeframe}) — failed bullish OB at {obLow:F5}-{obHigh:F5}",
                                candles[k].Timestamp));
                            break;
                        }
                    }
                }
            }

            // Look for a Bearish OB that gets broken (becomes Bullish Breaker)
            if (isCandidateBullish)
            {
                int bearishCount = 0;
                for (int j = i + 1; j < candles.Count && candles[j].Close < candles[j].Open; j++)
                {
                    bearishCount++;
                }

                if (bearishCount >= MinImpulseCandles)
                {
                    decimal obHigh = Math.Max(candidate.Open, candidate.Close);

                    int afterImpulse = i + 1 + bearishCount;
                    for (int k = afterImpulse; k < candles.Count; k++)
                    {
                        if (candles[k].Close > obHigh)
                        {
                            decimal obLow = Math.Min(candidate.Open, candidate.Close);
                            patterns.Add(new DetectedPattern(
                                IctPatternType.BreakerBlock,
                                timeframe,
                                candles[k].Close,
                                ZoneHigh: obHigh,
                                ZoneLow: obLow,
                                $"Bullish Breaker Block on {symbol} ({timeframe}) — failed bearish OB at {obLow:F5}-{obHigh:F5}",
                                candles[k].Timestamp));
                            break;
                        }
                    }
                }
            }
        }

        return patterns;
    }
}
