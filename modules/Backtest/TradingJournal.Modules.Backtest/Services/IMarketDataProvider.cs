namespace TradingJournal.Modules.Backtest.Services;

public record OhlcvCandleData(
    DateTime Timestamp,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);

public interface IMarketDataProvider
{
    /// <summary>
    /// Downloads OHLCV historical data for the given asset and date range.
    /// Returns candles in chronological order.
    /// </summary>
    Task<List<OhlcvCandleData>> DownloadOhlcvAsync(
        string asset,
        Timeframe timeframe,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps internal Timeframe enum to the provider's interval string.
    /// </summary>
    string GetIntervalString(Timeframe timeframe);
}
