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
        // For M1, no aggregation needed — return raw data. These rows are read-only here,
        // so skip change tracking.
        if (targetTimeframe == Timeframe.M1)
        {
            return await context.OhlcvCandles
                .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                            && c.Timestamp >= fromTimestamp && c.Timestamp <= toTimestamp)
                .OrderBy(c => c.Timestamp)
                .AsNoTracking()
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

        // Group into time buckets and aggregate. m1Candles is ordered by timestamp, and
        // LINQ GroupBy preserves source order within each group, so AggregateBucket's
        // single-pass fold sees first/last in chronological order for Open/Close.
        List<OhlcvCandle> aggregated = m1Candles
            .GroupBy(c => FloorTimestamp(c.Timestamp, bucketMinutes))
            .OrderBy(g => g.Key)
            .Select(g => AggregateBucket(symbol, targetTimeframe, g.Key, g))
            .ToList();

        logger.LogDebug(
            "Aggregated {M1Count} M1 candles into {AggCount} {Timeframe} candles for {Symbol}",
            m1Candles.Count, aggregated.Count, targetTimeframe, symbol);

        return aggregated;
    }

    public async Task<List<OhlcvCandle>> AggregatePagedAsync(
        string symbol,
        Timeframe targetTimeframe,
        DateTime fromTimestamp,
        DateTime toTimestamp,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        int skip = (page - 1) * pageSize;

        IQueryable<OhlcvCandle> m1 = context.OhlcvCandles
            .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                        && c.Timestamp >= fromTimestamp && c.Timestamp <= toTimestamp);

        // For M1 no bucketing is needed — page the raw rows directly in SQL.
        if (targetTimeframe == Timeframe.M1)
        {
            return await m1
                .OrderBy(c => c.Timestamp)
                .Skip(skip)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        int bucketMinutes = (int)targetTimeframe;

        // Step 1: bucket and aggregate the order-independent columns (High/Low/Volume)
        // entirely in SQL, then page with OFFSET/FETCH. The bucket key is the integer
        // count of whole bucket-widths since DateTime.MinValue — the same midnight-aligned
        // boundary FloorTimestamp computes, so results match the in-memory path exactly.
        // Only `pageSize` rows (one per display candle) ever leave the database.
        List<BucketAggregate> buckets = await m1
            .GroupBy(c => EF.Functions.DateDiffMinute(DateTime.MinValue, c.Timestamp) / bucketMinutes)
            .Select(g => new BucketAggregate
            {
                BucketIndex = g.Key,
                High = g.Max(c => c.High),
                Low = g.Min(c => c.Low),
                Volume = g.Sum(c => c.Volume),
                OpenTime = g.Min(c => c.Timestamp),
                CloseTime = g.Max(c => c.Timestamp)
            })
            .OrderBy(b => b.BucketIndex)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (buckets.Count == 0)
            return [];

        // Step 2: Open/Close are order-dependent (first/last M1 in the bucket), which a
        // GROUP BY can't express. Fetch just the boundary M1 rows for this page — at most
        // 2 * pageSize rows — and map their Open/Close prices back onto each bucket.
        HashSet<DateTime> edgeTimestamps = [.. buckets.Select(b => b.OpenTime), .. buckets.Select(b => b.CloseTime)];

        Dictionary<DateTime, (decimal Open, decimal Close)> edges = await context.OhlcvCandles
            .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                        && edgeTimestamps.Contains(c.Timestamp))
            .Select(c => new { c.Timestamp, c.Open, c.Close })
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Timestamp, c => (c.Open, c.Close), cancellationToken);

        List<OhlcvCandle> aggregated = buckets
            .Select(b => new OhlcvCandle
            {
                Id = 0,
                Asset = symbol,
                Timeframe = targetTimeframe,
                Timestamp = DateTime.MinValue.AddMinutes((long)b.BucketIndex * bucketMinutes),
                Open = edges[b.OpenTime].Open,
                High = b.High,
                Low = b.Low,
                Close = edges[b.CloseTime].Close,
                Volume = b.Volume
            })
            .ToList();

        logger.LogDebug(
            "Aggregated page {Page} (size {PageSize}) into {AggCount} {Timeframe} candles for {Symbol}",
            page, pageSize, aggregated.Count, targetTimeframe, symbol);

        return aggregated;
    }

    public async Task<OhlcvCandle?> GetNextAggregatedCandleAsync(
        string symbol,
        Timeframe targetTimeframe,
        DateTime afterTimestamp,
        CancellationToken cancellationToken = default)
    {
        // For M1, just get the next raw candle — no bucket boundaries needed.
        if (targetTimeframe == Timeframe.M1)
        {
            return await context.OhlcvCandles
                .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                            && c.Timestamp > afterTimestamp)
                .OrderBy(c => c.Timestamp)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);
        }

        int bucketMinutes = (int)targetTimeframe;
        DateTime nextBucketStart = FloorTimestamp(afterTimestamp, bucketMinutes).AddMinutes(bucketMinutes);

        // Markets that close (indices, stocks, forex weekends) leave empty buckets between
        // sessions. Probing only the immediately-adjacent bucket would return null the moment
        // playback reaches an overnight/weekend/holiday gap and prematurely end the session.
        // Instead, find the first M1 candle AT OR AFTER the next bucket boundary and snap to
        // the bucket that candle belongs to — skipping any empty buckets in between. If none
        // remain, the asset's data is genuinely exhausted and null correctly ends the session.
        DateTime? firstTimestamp = await context.OhlcvCandles
            .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                        && c.Timestamp >= nextBucketStart)
            .OrderBy(c => c.Timestamp)
            .Select(c => (DateTime?)c.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstTimestamp is null)
            return null;

        nextBucketStart = FloorTimestamp(firstTimestamp.Value, bucketMinutes);
        DateTime nextBucketEnd = nextBucketStart.AddMinutes(bucketMinutes);

        IQueryable<OhlcvCandle> bucket = context.OhlcvCandles
            .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                        && c.Timestamp >= nextBucketStart && c.Timestamp < nextBucketEnd);

        // High/Low/Volume + the bucket's boundary timestamps collapse to a single
        // GROUP BY row in SQL — the M1 rows themselves never leave the database.
        BucketAggregate? agg = await bucket
            .GroupBy(_ => 1)
            .Select(g => new BucketAggregate
            {
                BucketIndex = 0,
                High = g.Max(c => c.High),
                Low = g.Min(c => c.Low),
                Volume = g.Sum(c => c.Volume),
                OpenTime = g.Min(c => c.Timestamp),
                CloseTime = g.Max(c => c.Timestamp)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (agg is null)
            return null;

        // Open/Close are order-dependent (first/last M1), which a GROUP BY can't
        // express — fetch just the two boundary rows.
        Dictionary<DateTime, (decimal Open, decimal Close)> edges = await context.OhlcvCandles
            .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                        && (c.Timestamp == agg.OpenTime || c.Timestamp == agg.CloseTime))
            .Select(c => new { c.Timestamp, c.Open, c.Close })
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Timestamp, c => (c.Open, c.Close), cancellationToken);

        return new OhlcvCandle
        {
            Id = 0,
            Asset = symbol,
            Timeframe = targetTimeframe,
            Timestamp = nextBucketStart,
            Open = edges[agg.OpenTime].Open,
            High = agg.High,
            Low = agg.Low,
            Close = edges[agg.CloseTime].Close,
            Volume = agg.Volume
        };
    }

    public async Task<NextBucket?> GetNextBucketWithM1Async(
        string symbol,
        Timeframe targetTimeframe,
        DateTime afterTimestamp,
        CancellationToken cancellationToken = default)
    {
        int bucketMinutes = (int)targetTimeframe;
        DateTime nextBucketStart = FloorTimestamp(afterTimestamp, bucketMinutes).AddMinutes(bucketMinutes);

        // Skip empty buckets (closed-market gaps) by snapping to the bucket of the next
        // available M1 candle — see GetNextAggregatedCandleAsync for the rationale.
        DateTime? firstTimestamp = await context.OhlcvCandles
            .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                        && c.Timestamp >= nextBucketStart)
            .OrderBy(c => c.Timestamp)
            .Select(c => (DateTime?)c.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstTimestamp is null)
            return null;

        nextBucketStart = FloorTimestamp(firstTimestamp.Value, bucketMinutes);
        DateTime nextBucketEnd = nextBucketStart.AddMinutes(bucketMinutes);

        // Single read of the bucket's M1 window. The same list is used to derive the
        // display candle here AND to drive intra-bar order matching at the call site.
        List<OhlcvCandle> m1Candles = await context.OhlcvCandles
            .Where(c => c.Asset == symbol && c.Timeframe == Timeframe.M1
                        && c.Timestamp >= nextBucketStart && c.Timestamp < nextBucketEnd)
            .OrderBy(c => c.Timestamp)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (m1Candles.Count == 0)
            return null;

        OhlcvCandle displayCandle = AggregateBucket(symbol, targetTimeframe, nextBucketStart, m1Candles);
        return new NextBucket(displayCandle, m1Candles);
    }

    /// <summary>
    /// Computes a display candle's O/H/L/C/V from an ordered, non-empty sequence of the
    /// M1 candles in its bucket. Open = first, Close = last, High/Low/Volume aggregated.
    /// Single pass over the sequence — one enumeration, not five.
    /// </summary>
    private static OhlcvCandle AggregateBucket(
        string symbol, Timeframe targetTimeframe, DateTime bucketStart, IEnumerable<OhlcvCandle> m1Candles)
    {
        decimal open = 0m, high = decimal.MinValue, low = decimal.MaxValue, close = 0m, volume = 0m;
        bool first = true;

        foreach (OhlcvCandle c in m1Candles)
        {
            if (first) { open = c.Open; first = false; }
            if (c.High > high) high = c.High;
            if (c.Low < low) low = c.Low;
            close = c.Close;
            volume += c.Volume;
        }

        return new OhlcvCandle
        {
            Id = 0,
            Asset = symbol,
            Timeframe = targetTimeframe,
            Timestamp = bucketStart,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume
        };
    }

    /// <summary>
    /// SQL-side aggregate of one display-candle bucket: the columns a GROUP BY can
    /// compute directly, plus the boundary timestamps used to recover Open/Close.
    /// </summary>
    private sealed class BucketAggregate
    {
        public int BucketIndex { get; init; }
        public decimal High { get; init; }
        public decimal Low { get; init; }
        public decimal Volume { get; init; }
        public DateTime OpenTime { get; init; }
        public DateTime CloseTime { get; init; }
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
