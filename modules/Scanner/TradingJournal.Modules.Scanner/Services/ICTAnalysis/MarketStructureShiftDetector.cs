namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Market Structure Shift (MSS) — a decisive break of a significant
/// swing point that confirms a change in market trend direction.
///
/// Unlike CHoCH (first break of immediate swing), MSS requires:
/// 1. A sequence of at least 2 confirmed swing points defining trend
/// 2. Break of a significant (non-immediate) swing high/low
/// 3. A strong displacement candle doing the breaking
///
/// Bullish MSS: Break above a significant swing high after a downtrend.
/// Bearish MSS: Break below a significant swing low after an uptrend.
/// </summary>
internal sealed class MarketStructureShiftDetector : IIctDetector
{
    /// <summary>
    /// ATR multiplier — the breaking candle's range must be at least this × ATR
    /// to qualify as a displacement/strong break.
    /// </summary>
    private const decimal MinBreakStrength = 1.0m;

    public IctPatternType PatternType => IctPatternType.MarketStructureShift;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 15) return patterns;

        var swingHighs = IctHelpers.FindSwingHighs(candles);
        var swingLows = IctHelpers.FindSwingLows(candles);

        if (swingHighs.Count < 3 || swingLows.Count < 3) return patterns;

        decimal atr = IctHelpers.CalculateAtr(candles);
        if (atr <= 0) return patterns;

        // Bullish MSS: downtrend (sequence of lower lows), then strong break of a prior swing high
        // Look at the 2nd-to-last swing high (significant level, not the most recent)
        var significantHigh = swingHighs[^2];

        // Verify downtrend: at least 2 consecutive lower lows
        bool hasDowntrend = false;
        for (int s = swingLows.Count - 1; s >= 1; s--)
        {
            if (swingLows[s].Price < swingLows[s - 1].Price &&
                swingLows[s].Index > significantHigh.Index)
            {
                hasDowntrend = true;
                break;
            }
        }

        if (hasDowntrend)
        {
            for (int i = significantHigh.Index + 3; i < candles.Count; i++)
            {
                if (candles[i].Close > significantHigh.Price &&
                    IctHelpers.Range(candles[i]) >= atr * MinBreakStrength)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.MarketStructureShift,
                        timeframe,
                        candles[i].Close,
                        ZoneHigh: significantHigh.Price,
                        ZoneLow: swingLows[^1].Price,
                        $"Bullish MSS on {symbol} ({timeframe}) — broke significant high {significantHigh.Price:F5}",
                        candles[i].Timestamp));
                    break;
                }
            }
        }

        // Bearish MSS: uptrend (sequence of higher highs), then strong break of a prior swing low
        var significantLow = swingLows[^2];

        bool hasUptrend = false;
        for (int s = swingHighs.Count - 1; s >= 1; s--)
        {
            if (swingHighs[s].Price > swingHighs[s - 1].Price &&
                swingHighs[s].Index > significantLow.Index)
            {
                hasUptrend = true;
                break;
            }
        }

        if (hasUptrend)
        {
            for (int i = significantLow.Index + 3; i < candles.Count; i++)
            {
                if (candles[i].Close < significantLow.Price &&
                    IctHelpers.Range(candles[i]) >= atr * MinBreakStrength)
                {
                    patterns.Add(new DetectedPattern(
                        IctPatternType.MarketStructureShift,
                        timeframe,
                        candles[i].Close,
                        ZoneHigh: swingHighs[^1].Price,
                        ZoneLow: significantLow.Price,
                        $"Bearish MSS on {symbol} ({timeframe}) — broke significant low {significantLow.Price:F5}",
                        candles[i].Timestamp));
                    break;
                }
            }
        }

        return patterns;
    }
}
