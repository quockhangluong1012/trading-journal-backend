namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Order Blocks — the last opposite candle before a strong impulsive move.
///
/// Bullish OB: Last bearish candle before 3+ consecutive bullish candles (impulse up).
/// Bearish OB: Last bullish candle before 3+ consecutive bearish candles (impulse down).
///
/// The OB zone is defined by the body of the last opposite candle.
/// </summary>
internal sealed class OrderBlockDetector : IIctDetector
{
    private const int MinImpulseCandles = 3;

    public IctPatternType PatternType => IctPatternType.OrderBlock;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < MinImpulseCandles + 1) return patterns;

        int startIndex = Math.Max(0, candles.Count - 50);

        for (int i = startIndex; i < candles.Count - MinImpulseCandles; i++)
        {
            CandleData candidate = candles[i];
            bool isCandidateBearish = candidate.Close < candidate.Open;
            bool isCandidateBullish = candidate.Close > candidate.Open;

            // Check for bullish impulse after a bearish candle (Bullish OB)
            if (isCandidateBearish)
            {
                int bullishCount = 0;
                for (int j = i + 1; j < candles.Count && candles[j].Close > candles[j].Open; j++)
                {
                    bullishCount++;
                }

                if (bullishCount >= MinImpulseCandles)
                {
                    decimal obHigh = Math.Max(candidate.Open, candidate.Close);
                    decimal obLow = Math.Min(candidate.Open, candidate.Close);

                    patterns.Add(new DetectedPattern(
                        IctPatternType.OrderBlock,
                        timeframe,
                        candles[i + bullishCount].Close,
                        ZoneHigh: obHigh,
                        ZoneLow: obLow,
                        $"Bullish Order Block on {symbol} ({timeframe}) — zone {obLow:F5} to {obHigh:F5}",
                        candles[i + bullishCount].Timestamp));
                }
            }

            // Check for bearish impulse after a bullish candle (Bearish OB)
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
                    decimal obLow = Math.Min(candidate.Open, candidate.Close);

                    patterns.Add(new DetectedPattern(
                        IctPatternType.OrderBlock,
                        timeframe,
                        candles[i + bearishCount].Close,
                        ZoneHigh: obHigh,
                        ZoneLow: obLow,
                        $"Bearish Order Block on {symbol} ({timeframe}) — zone {obLow:F5} to {obHigh:F5}",
                        candles[i + bearishCount].Timestamp));
                }
            }
        }

        return patterns;
    }
}
