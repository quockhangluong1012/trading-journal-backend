namespace TradingJournal.Modules.Backtest.Services;

public record PlaybackAdvanceResult(
    OhlcvCandle? Candle,
    MatchingResult? MatchingResult,
    decimal UpdatedBalance,
    DateTime NewTimestamp,
    bool IsSessionEnded,
    // The orders the engine just filled/closed this advance, in the state it left them.
    // They are already tracked in the request's DbContext, so callers map these directly
    // instead of re-fetching each one by id.
    IReadOnlyList<BacktestOrder>? FilledOrders = null,
    IReadOnlyList<BacktestOrder>? ClosedOrders = null);

public interface IPlaybackEngine
{
    /// <summary>
    /// Advances the playback by one display candle.
    /// Internally uses M1 candles for accurate intra-bar order evaluation.
    /// </summary>
    Task<PlaybackAdvanceResult> AdvanceCandleAsync(int sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the playback speed multiplier (x1, x2, x5, x10).
    /// Speed affects the delay between auto-advance ticks in Play mode.
    /// </summary>
    Task UpdatePlaybackSpeedAsync(int sessionId, int speed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the display timeframe for playback (multi-timeframe sync).
    /// The current timestamp is preserved — only the candle aggregation changes.
    /// All stored M1 data is re-aggregated to the new timeframe.
    /// </summary>
    Task ChangeTimeframeAsync(int sessionId, Timeframe newTimeframe, CancellationToken cancellationToken = default);
}
