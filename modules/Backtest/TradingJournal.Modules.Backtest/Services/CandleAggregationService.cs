using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Aggregates M1 candles into higher timeframes on-the-fly.
/// Only M1 candles are stored in the database — this service computes
/// M5, M15, H1, H4, D1 by grouping M1 candles into time buckets.
///
/// Algorithm:
///   1. Query M1 candles from DB in the given time range
///   2. Round each M1 timestamp down to the nearest bucket boundary
///   3. Group by bucket, then compute: Open=first, High=max, Low=min, Close=last, Vol=sum
/// </summary>
internal sealed class CandleAggregationService(
    IBacktestDbContext context,
    ILogger<CandleAggregationService> logger) : ICandleAggregationService
{
    public async Task<List<OhlcvCandle>> AggregateAsync(
        string symbol,
        Timeframe targetTimeframe,
        DateTime fromTimestamp,
        DateTime toTimestamp,
        CancellationToken cancellationToken = default)
    {
        // For M1, no aggregation needed — return raw data
        if (targetTimeframe == Timeframe.M1)
        {
            return await context.OhlcvCandles
                .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                            && c.Timestamp >= fromTimestamp && c.Timestamp <= toTimestamp)
                .OrderBy(c => c.Timestamp)
                .ToListAsync(cancellationToken);
        }

        int bucketMinutes = (int)targetTimeframe;

        // Load M1 candles from DB
        List<OhlcvCandle> m1Candles = await context.OhlcvCandles
            .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                        && c.Timestamp >= fromTimestamp && c.Timestamp <= toTimestamp)
            .OrderBy(c => c.Timestamp)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (m1Candles.Count == 0)
            return [];

        // Group into time buckets and aggregate
        List<OhlcvCandle> aggregated = m1Candles
            .GroupBy(c => FloorTimestamp(c.Timestamp, bucketMinutes))
            .OrderBy(g => g.Key)
            .Select(g => new OhlcvCandle
            {
                Id = 0,
                Asset = symbol,
                Timeframe = targetTimeframe,
                Timestamp = g.Key,
                Open = g.First().Open,
                High = g.Max(c => c.High),
                Low = g.Min(c => c.Low),
                Close = g.Last().Close,
                Volume = g.Sum(c => c.Volume)
            })
            .ToList();

        logger.LogDebug(
            "Aggregated {M1Count} M1 candles into {AggCount} {Timeframe} candles for {Symbol}",
            m1Candles.Count, aggregated.Count, targetTimeframe, symbol);

        return aggregated;
    }

    public async Task<OhlcvCandle?> GetNextAggregatedCandleAsync(
        string symbol,
        Timeframe targetTimeframe,
        DateTime afterTimestamp,
        CancellationToken cancellationToken = default)
    {
        int bucketMinutes = (int)targetTimeframe;

        // Calculate the next bucket start time
        DateTime currentBucketStart = FloorTimestamp(afterTimestamp, bucketMinutes);
        DateTime nextBucketStart = currentBucketStart.AddMinutes(bucketMinutes);
        DateTime nextBucketEnd = nextBucketStart.AddMinutes(bucketMinutes).AddSeconds(-1);

        // For M1, just get the next raw candle
        if (targetTimeframe == Timeframe.M1)
        {
            return await context.OhlcvCandles
                .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                            && c.Timestamp > afterTimestamp)
                .OrderBy(c => c.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Load M1 candles for the next bucket
        List<OhlcvCandle> m1Candles = await context.OhlcvCandles
            .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                        && c.Timestamp >= nextBucketStart && c.Timestamp < nextBucketStart.AddMinutes(bucketMinutes))
            .OrderBy(c => c.Timestamp)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (m1Candles.Count == 0)
            return null;

        return new OhlcvCandle
        {
            Id = 0,
            Asset = symbol,
            Timeframe = targetTimeframe,
            Timestamp = nextBucketStart,
            Open = m1Candles.First().Open,
            High = m1Candles.Max(c => c.High),
            Low = m1Candles.Min(c => c.Low),
            Close = m1Candles.Last().Close,
            Volume = m1Candles.Sum(c => c.Volume)
        };
    }

    /// <summary>
    /// Floors a DateTime to the nearest bucket boundary.
    /// For M5:  12:37 → 12:35
    /// For M15: 12:37 → 12:30
    /// For H1:  12:37 → 12:00
    /// For H4:  14:37 → 12:00 (4h blocks: 0, 4, 8, 12, 16, 20)
    /// For D1:  any   → 00:00
    /// </summary>
    private static DateTime FloorTimestamp(DateTime timestamp, int bucketMinutes)
    {
        long ticks = timestamp.Ticks;
        long bucketTicks = TimeSpan.FromMinutes(bucketMinutes).Ticks;
        long floored = ticks - (ticks % bucketTicks);
        return new DateTime(floored, DateTimeKind.Utc);
    }
}
