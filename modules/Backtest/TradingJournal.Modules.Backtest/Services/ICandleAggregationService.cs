namespace TradingJournal.Modules.Backtest.Services;

public interface ICandleAggregationService
{
    /// <summary>
    /// Aggregates M1 candles from the database into the target timeframe.
    /// Groups candles into time buckets and computes OHLCV for each bucket.
    /// </summary>
    Task<List<OhlcvCandle>> AggregateAsync(
        string symbol,
        Timeframe targetTimeframe,
        DateTime fromTimestamp,
        DateTime toTimestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregates M1 candles into the target timeframe and returns a single page,
    /// pushing the bucketing (GROUP BY on a computed bucket key) and the paging
    /// (OFFSET/FETCH) into SQL. Only the requested page of display candles is
    /// materialized — the full session history is never loaded into memory.
    /// </summary>
    Task<List<OhlcvCandle>> AggregatePagedAsync(
        string symbol,
        Timeframe targetTimeframe,
        DateTime fromTimestamp,
        DateTime toTimestamp,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next candle after the given timestamp in the target timeframe.
    /// Used by the playback engine to advance one candle when no intra-bar M1
    /// replay is needed. Computes O/H/L/C/V with a SQL aggregate (a single
    /// returned row), so the bucket's M1 rows are never materialized.
    /// </summary>
    Task<OhlcvCandle?> GetNextAggregatedCandleAsync(
        string symbol,
        Timeframe targetTimeframe,
        DateTime afterTimestamp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next display candle AND the underlying M1 rows of its bucket in a
    /// single DB read. Used by the playback engine on the hot path: when intra-bar
    /// M1 replay is required, the same list feeds both the display candle's O/H/L/C/V
    /// (computed in code) and the order matcher — avoiding a second read of the bucket.
    /// Returns null when the next bucket has no M1 data.
    /// </summary>
    Task<NextBucket?> GetNextBucketWithM1Async(
        string symbol,
        Timeframe targetTimeframe,
        DateTime afterTimestamp,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The next display candle together with the M1 candles it was aggregated from,
/// both produced by a single read of the bucket's M1 window.
/// </summary>
public sealed record NextBucket(OhlcvCandle DisplayCandle, List<OhlcvCandle> M1Candles);
