namespace TradingJournal.Modules.Backtest.Services;

public record PlaybackAdvanceResult(
    OhlcvCandle? Candle,
    MatchingResult? MatchingResult,
    decimal UpdatedBalance,
    DateTime NewTimestamp,
    bool IsSessionEnded);

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
