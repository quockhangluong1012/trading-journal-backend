namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Shared utility methods for ICT pattern detection.
/// Extracts common swing-point and candle-analysis logic used by multiple detectors.
/// </summary>
internal static class IctHelpers
{
    /// <summary>
    /// Finds swing highs using a 2-bar lookback/forward confirmation.
    /// </summary>
    public static List<(decimal Price, int Index, DateTimeOffset Timestamp)> FindSwingHighs(
        IReadOnlyList<CandleData> candles, int startIndex = 2, int? endIndex = null)
    {
        var highs = new List<(decimal, int, DateTimeOffset)>();
        int end = endIndex ?? candles.Count - 2;

        for (int i = Math.Max(2, startIndex); i < Math.Min(candles.Count - 2, end); i++)
        {
            if (candles[i].High > candles[i - 1].High &&
                candles[i].High > candles[i - 2].High &&
                candles[i].High > candles[i + 1].High &&
                candles[i].High > candles[i + 2].High)
            {
                highs.Add((candles[i].High, i, candles[i].Timestamp));
            }
        }

        return highs;
    }

    /// <summary>
    /// Finds swing lows using a 2-bar lookback/forward confirmation.
    /// </summary>
    public static List<(decimal Price, int Index, DateTimeOffset Timestamp)> FindSwingLows(
        IReadOnlyList<CandleData> candles, int startIndex = 2, int? endIndex = null)
    {
        var lows = new List<(decimal, int, DateTimeOffset)>();
        int end = endIndex ?? candles.Count - 2;

        for (int i = Math.Max(2, startIndex); i < Math.Min(candles.Count - 2, end); i++)
        {
            if (candles[i].Low < candles[i - 1].Low &&
                candles[i].Low < candles[i - 2].Low &&
                candles[i].Low < candles[i + 1].Low &&
                candles[i].Low < candles[i + 2].Low)
            {
                lows.Add((candles[i].Low, i, candles[i].Timestamp));
            }
        }

        return lows;
    }

    /// <summary>
    /// Detects raw FVG zones (used by iFVG, BPR, Unicorn, Venom detectors).
    /// Returns (zoneHigh, zoneLow, isBullish, middleCandleIndex).
    /// </summary>
    public static List<(decimal ZoneHigh, decimal ZoneLow, bool IsBullish, int Index)> FindFvgZones(
        IReadOnlyList<CandleData> candles, int startIndex = 0)
    {
        var zones = new List<(decimal, decimal, bool, int)>();

        for (int i = Math.Max(0, startIndex); i <= candles.Count - 3; i++)
        {
            CandleData c0 = candles[i];
            CandleData c1 = candles[i + 1];
            CandleData c2 = candles[i + 2];

            decimal bodySize = Math.Abs(c1.Close - c1.Open);

            // Bullish FVG
            if (c2.Low > c0.High)
            {
                decimal gapSize = c2.Low - c0.High;
                if (bodySize > 0 && gapSize / bodySize > 0.2m)
                {
                    zones.Add((c2.Low, c0.High, true, i + 1));
                }
            }

            // Bearish FVG
            if (c2.High < c0.Low)
            {
                decimal gapSize = c0.Low - c2.High;
                if (bodySize > 0 && gapSize / bodySize > 0.2m)
                {
                    zones.Add((c0.Low, c2.High, false, i + 1));
                }
            }
        }

        return zones;
    }

    /// <summary>
    /// Finds Order Block zones (last opposite candle before impulse).
    /// Returns (obHigh, obLow, isBullish, obIndex, impulseEndIndex).
    /// </summary>
    public static List<(decimal High, decimal Low, bool IsBullish, int ObIndex, int ImpulseEnd)> FindOrderBlockZones(
        IReadOnlyList<CandleData> candles, int minImpulseCandles = 3, int startIndex = 0)
    {
        var zones = new List<(decimal, decimal, bool, int, int)>();

        for (int i = Math.Max(0, startIndex); i < candles.Count - minImpulseCandles; i++)
        {
            CandleData candidate = candles[i];
            bool isBearish = candidate.Close < candidate.Open;
            bool isBullish = candidate.Close > candidate.Open;

            if (isBearish)
            {
                int bullishCount = 0;
                for (int j = i + 1; j < candles.Count && candles[j].Close > candles[j].Open; j++)
                    bullishCount++;

                if (bullishCount >= minImpulseCandles)
                {
                    decimal obHigh = Math.Max(candidate.Open, candidate.Close);
                    decimal obLow = Math.Min(candidate.Open, candidate.Close);
                    zones.Add((obHigh, obLow, true, i, i + bullishCount));
                }
            }

            if (isBullish)
            {
                int bearishCount = 0;
                for (int j = i + 1; j < candles.Count && candles[j].Close < candles[j].Open; j++)
                    bearishCount++;

                if (bearishCount >= minImpulseCandles)
                {
                    decimal obHigh = Math.Max(candidate.Open, candidate.Close);
                    decimal obLow = Math.Min(candidate.Open, candidate.Close);
                    zones.Add((obHigh, obLow, false, i, i + bearishCount));
                }
            }
        }

        return zones;
    }

    /// <summary>
    /// Calculates the Average True Range over the specified period.
    /// </summary>
    public static decimal CalculateAtr(IReadOnlyList<CandleData> candles, int period = 14)
    {
        if (candles.Count < period + 1) return 0;

        decimal sum = 0;
        for (int i = candles.Count - period; i < candles.Count; i++)
        {
            decimal tr = Math.Max(
                candles[i].High - candles[i].Low,
                Math.Max(
                    Math.Abs(candles[i].High - candles[i - 1].Close),
                    Math.Abs(candles[i].Low - candles[i - 1].Close)));
            sum += tr;
        }

        return sum / period;
    }

    /// <summary>
    /// Checks if a candle is bullish (close > open).
    /// </summary>
    public static bool IsBullish(CandleData candle) => candle.Close > candle.Open;

    /// <summary>
    /// Checks if a candle is bearish (close < open).
    /// </summary>
    public static bool IsBearish(CandleData candle) => candle.Close < candle.Open;

    /// <summary>
    /// Gets the body size of a candle.
    /// </summary>
    public static decimal BodySize(CandleData candle) => Math.Abs(candle.Close - candle.Open);

    /// <summary>
    /// Gets the full range (high - low) of a candle.
    /// </summary>
    public static decimal Range(CandleData candle) => candle.High - candle.Low;
}
