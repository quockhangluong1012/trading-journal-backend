namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects the Venom Model — a liquidity sweep that drives price into an
/// Order Block containing a Fair Value Gap, creating a high-probability reversal setup.
///
/// Bullish Venom: Liquidity sweep below a swing low into a bullish OB that has an FVG.
/// Bearish Venom: Liquidity sweep above a swing high into a bearish OB that has an FVG.
/// </summary>
internal sealed class VenomModelDetector : IIctDetector
{
    private const decimal TolerancePercent = 0.001m;

    public IctPatternType PatternType => IctPatternType.VenomModel;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 15) return patterns;

        int startIndex = Math.Max(0, candles.Count - 50);

        // Get the building blocks
        var obZones = IctHelpers.FindOrderBlockZones(candles, 3, startIndex);
        var fvgZones = IctHelpers.FindFvgZones(candles, startIndex);
        var swingHighs = IctHelpers.FindSwingHighs(candles, startIndex);
        var swingLows = IctHelpers.FindSwingLows(candles, startIndex);

        // Find OBs that contain an FVG
        foreach (var (obHigh, obLow, isBullishOb, obIndex, impulseEnd) in obZones)
        {
            // Check if there's an FVG within or near this OB zone
            bool hasOverlappingFvg = fvgZones.Any(fvg =>
                fvg.IsBullish == isBullishOb &&
                Math.Min(fvg.ZoneHigh, obHigh) > Math.Max(fvg.ZoneLow, obLow));

            if (!hasOverlappingFvg) continue;

            // Now check for a liquidity sweep that drives price into this OB
            int checkStart = Math.Max(impulseEnd, candles.Count - 10);

            for (int i = checkStart; i < candles.Count; i++)
            {
                CandleData candle = candles[i];

                if (isBullishOb)
                {
                    // Bullish Venom: sweep below swing low into bullish OB
                    bool sweptLow = swingLows.Any(sl =>
                        sl.Index < i &&
                        candle.Low < sl.Price - sl.Price * TolerancePercent);

                    bool inObZone = candle.Low <= obHigh && candle.Low >= obLow;
                    bool reversed = candle.Close > obLow && IctHelpers.IsBullish(candle);

                    if (sweptLow && inObZone && reversed)
                    {
                        patterns.Add(new DetectedPattern(
                            IctPatternType.VenomModel,
                            timeframe,
                            candle.Close,
                            ZoneHigh: obHigh,
                            ZoneLow: obLow,
                            $"Bullish Venom Model on {symbol} ({timeframe}) — sweep into OB+FVG at {obLow:F5}-{obHigh:F5}",
                            candle.Timestamp));
                        break;
                    }
                }
                else
                {
                    // Bearish Venom: sweep above swing high into bearish OB
                    bool sweptHigh = swingHighs.Any(sh =>
                        sh.Index < i &&
                        candle.High > sh.Price + sh.Price * TolerancePercent);

                    bool inObZone = candle.High >= obLow && candle.High <= obHigh;
                    bool reversed = candle.Close < obHigh && IctHelpers.IsBearish(candle);

                    if (sweptHigh && inObZone && reversed)
                    {
                        patterns.Add(new DetectedPattern(
                            IctPatternType.VenomModel,
                            timeframe,
                            candle.Close,
                            ZoneHigh: obHigh,
                            ZoneLow: obLow,
                            $"Bearish Venom Model on {symbol} ({timeframe}) — sweep into OB+FVG at {obLow:F5}-{obHigh:F5}",
                            candle.Timestamp));
                        break;
                    }
                }
            }
        }

        return patterns;
    }
}
