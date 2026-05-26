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
    /// Gets the next candle after the given timestamp in the target timeframe.
    /// Used by the playback engine to advance one candle at a time.
    /// </summary>
    Task<OhlcvCandle?> GetNextAggregatedCandleAsync(
        string symbol,
        Timeframe targetTimeframe,
        DateTime afterTimestamp,
        CancellationToken cancellationToken = default);
}
