using TradingJournal.Modules.Scanner.Services.ICTAnalysis;

namespace TradingJournal.Modules.Scanner.Services.LiveData;

/// <summary>
/// Provides live OHLCV candle data from external market data APIs.
/// Implementations should handle caching and rate limiting internally.
/// </summary>
public interface ILiveMarketDataProvider
{
    /// <summary>
    /// Fetches the most recent candles for a given symbol and timeframe.
    /// Returns candles ordered chronologically (oldest first).
    /// </summary>
    /// <param name="symbol">The trading instrument (e.g., "EURUSD", "NQ", "XAUUSD").</param>
    /// <param name="timeframe">The candle interval.</param>
    /// <param name="count">Number of most recent candles to fetch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of candles ordered oldest-first, or empty if unavailable.</returns>
    Task<List<CandleData>> GetRecentCandlesAsync(
        string symbol,
        ScannerTimeframe timeframe,
        int count,
        CancellationToken ct = default);
}
