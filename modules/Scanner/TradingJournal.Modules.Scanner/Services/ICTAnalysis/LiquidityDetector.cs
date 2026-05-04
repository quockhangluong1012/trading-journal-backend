namespace TradingJournal.Modules.Scanner.Services.ICTAnalysis;

/// <summary>
/// Detects Liquidity pools — areas where equal highs or equal lows cluster,
/// forming likely stop-loss pools that smart money targets.
///
/// Equal highs/lows are identified when 2+ swing points exist at similar
/// price levels (within a configurable tolerance).
/// </summary>
internal sealed class LiquidityDetector : IIctDetector
{
    /// <summary>
    /// Price tolerance for considering two swing points "equal" (0.05% of price).
    /// </summary>
    private const decimal TolerancePercent = 0.0005m;

    /// <summary>
    /// Minimum number of equal swing points to form a liquidity pool.
    /// </summary>
    private const int MinEqualPoints = 2;

    public IctPatternType PatternType => IctPatternType.Liquidity;

    public List<DetectedPattern> Detect(IReadOnlyList<CandleData> candles, string symbol, ScannerTimeframe timeframe)
    {
        var patterns = new List<DetectedPattern>();

        if (candles.Count < 5) return patterns;

        List<(decimal Price, DateTimeOffset Timestamp)> swingHighs = FindSwingHighs(candles);
        List<(decimal Price, DateTimeOffset Timestamp)> swingLows = FindSwingLows(candles);

        // Find clusters of equal highs (buy-side liquidity)
        var highClusters = FindClusters(swingHighs);
        foreach (var cluster in highClusters)
        {
            decimal avgPrice = cluster.Average(c => c.Price);
            patterns.Add(new DetectedPattern(
                IctPatternType.Liquidity,
                timeframe,
                avgPrice,
                ZoneHigh: cluster.Max(c => c.Price),
                ZoneLow: cluster.Min(c => c.Price),
                $"Buy-side liquidity (equal highs) on {symbol} ({timeframe}) — {cluster.Count} touches at ~{avgPrice:F5}",
                cluster.Last().Timestamp));
        }

        // Find clusters of equal lows (sell-side liquidity)
        var lowClusters = FindClusters(swingLows);
        foreach (var cluster in lowClusters)
        {
            decimal avgPrice = cluster.Average(c => c.Price);
            patterns.Add(new DetectedPattern(
                IctPatternType.Liquidity,
                timeframe,
                avgPrice,
                ZoneHigh: cluster.Max(c => c.Price),
                ZoneLow: cluster.Min(c => c.Price),
                $"Sell-side liquidity (equal lows) on {symbol} ({timeframe}) — {cluster.Count} touches at ~{avgPrice:F5}",
                cluster.Last().Timestamp));
        }

        return patterns;
    }

    private static List<(decimal Price, DateTimeOffset Timestamp)> FindSwingHighs(IReadOnlyList<CandleData> candles)
    {
        var highs = new List<(decimal, DateTimeOffset)>();

        for (int i = 2; i < candles.Count - 2; i++)
        {
            if (candles[i].High > candles[i - 1].High &&
                candles[i].High > candles[i - 2].High &&
                candles[i].High > candles[i + 1].High &&
                candles[i].High > candles[i + 2].High)
            {
                highs.Add((candles[i].High, candles[i].Timestamp));
            }
        }

        return highs;
    }

    private static List<(decimal Price, DateTimeOffset Timestamp)> FindSwingLows(IReadOnlyList<CandleData> candles)
    {
        var lows = new List<(decimal, DateTimeOffset)>();

        for (int i = 2; i < candles.Count - 2; i++)
        {
            if (candles[i].Low < candles[i - 1].Low &&
                candles[i].Low < candles[i - 2].Low &&
                candles[i].Low < candles[i + 1].Low &&
                candles[i].Low < candles[i + 2].Low)
            {
                lows.Add((candles[i].Low, candles[i].Timestamp));
            }
        }

        return lows;
    }

    private static List<List<(decimal Price, DateTimeOffset Timestamp)>> FindClusters(
        List<(decimal Price, DateTimeOffset Timestamp)> points)
    {
        var clusters = new List<List<(decimal Price, DateTimeOffset Timestamp)>>();
        var used = new HashSet<int>();

        for (int i = 0; i < points.Count; i++)
        {
            if (used.Contains(i)) continue;

            var cluster = new List<(decimal Price, DateTimeOffset Timestamp)> { points[i] };
            decimal tolerance = points[i].Price * TolerancePercent;

            for (int j = i + 1; j < points.Count; j++)
            {
                if (used.Contains(j)) continue;

                if (Math.Abs(points[j].Price - points[i].Price) <= tolerance)
                {
                    cluster.Add(points[j]);
                    used.Add(j);
                }
            }

            if (cluster.Count >= MinEqualPoints)
            {
                clusters.Add(cluster);
            }

            used.Add(i);
        }

        return clusters;
    }
}
