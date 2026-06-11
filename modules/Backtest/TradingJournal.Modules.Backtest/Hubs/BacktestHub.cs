using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Backtest.Dto;

namespace TradingJournal.Modules.Backtest.Hubs;

/// <summary>
/// SignalR hub for real-time backtest playback control.
///
/// Client → Server methods:
///   - JoinSession(sessionId)       — join session group for updates
///   - LeaveSession(sessionId)      — leave session group
///   - Play(sessionId)              — start auto-advancing candles
///   - Pause(sessionId)             — stop auto-advancing
///   - Skip(sessionId)              — advance one candle manually
///   - SetSpeed(sessionId, speed)   — change speed (1, 2, 5, 10)
///   - SetTimeframe(sessionId, tf)  — switch display timeframe
///
/// Server → Client events:
///   - CandleAdvanced: { Candle, Balance, Equity, UnrealizedPnl, Timestamp, IsEnded, FilledOrders, ClosedPositions }
///   - PlaybackStateChanged: { SessionId, IsPlaying, Speed, Timeframe }
///   - DataProgress: { SessionId, Timeframe, CandleCount, TotalExpected }
///   - DataReady: { SessionId, TotalCandles }
///   - DataError: { SessionId, Error }
/// </summary>
[Authorize]
public sealed class BacktestHub(
    IServiceScopeFactory scopeFactory,
    ILogger<BacktestHub> logger) : Hub
{
    // Track playing sessions: sessionId → CancellationTokenSource
    private static readonly ConcurrentDictionary<int, CancellationTokenSource> PlayingSessions = new();

    // Track which connection started the play loop for a session, so we can stop
    // the loop when that connection disconnects: sessionId → connectionId
    private static readonly ConcurrentDictionary<int, string> SessionPlayers = new();

    // Current playback speed per actively-playing session: sessionId → speed.
    // Seeded once when the auto-advance loop starts and updated live by SetSpeed, so the
    // loop reads the delay from memory instead of opening a scope + querying every tick.
    private static readonly ConcurrentDictionary<int, int> SessionSpeeds = new();

    // Base delay in milliseconds between candle advances at x1 speed
    private const int BaseDelayMs = 1000;

    public async Task JoinSession(int sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"backtest-{sessionId}");
        logger.LogDebug("Connection {ConnectionId} joined session {SessionId}", Context.ConnectionId, sessionId);
    }

    public async Task LeaveSession(int sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"backtest-{sessionId}");

        // Stop playing if this was the last connection
        StopPlaying(sessionId);
        logger.LogDebug("Connection {ConnectionId} left session {SessionId}", Context.ConnectionId, sessionId);
    }

    /// <summary>
    /// Start auto-advancing candles. The server will continuously advance
    /// and push CandleAdvanced events until Pause is called or session ends.
    /// </summary>
    public async Task Play(int sessionId)
    {
        // Cancel any existing play loop for this session
        StopPlaying(sessionId);

        CancellationTokenSource cts = new();
        PlayingSessions[sessionId] = cts;
        SessionPlayers[sessionId] = Context.ConnectionId;

        // Notify clients
        await Clients.Group($"backtest-{sessionId}")
            .SendAsync("PlaybackStateChanged", new { SessionId = sessionId, IsPlaying = true });

        // Start the auto-advance loop in the background
        _ = Task.Run(() => AutoAdvanceLoop(sessionId, cts.Token), cts.Token);
    }

    /// <summary>
    /// Stop auto-advancing candles.
    /// </summary>
    public async Task Pause(int sessionId)
    {
        StopPlaying(sessionId);

        await Clients.Group($"backtest-{sessionId}")
            .SendAsync("PlaybackStateChanged", new { SessionId = sessionId, IsPlaying = false });
    }

    /// <summary>
    /// Advance exactly one candle (manual step).
    /// </summary>
    public async Task Skip(int sessionId)
    {
        // Pause any running auto-advance first
        StopPlaying(sessionId);

        await AdvanceAndNotify(sessionId);
    }

    /// <summary>
    /// Change playback speed. Affects the delay between auto-advance ticks.
    /// Valid speeds: 1, 2, 5, 10
    /// </summary>
    public async Task SetSpeed(int sessionId, int speed)
    {
        if (speed is not (1 or 2 or 5 or 10))
        {
            await Clients.Caller.SendAsync("Error", new { Message = "Speed must be 1, 2, 5, or 10." });
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        IPlaybackEngine engine = scope.ServiceProvider.GetRequiredService<IPlaybackEngine>();
        await engine.UpdatePlaybackSpeedAsync(sessionId, speed);

        // Push the new speed to a running auto-advance loop so it takes effect on the
        // next tick without a DB read. If nothing is playing, the loop reseeds from the
        // persisted value next time Play is called, so there's no entry to update.
        if (PlayingSessions.ContainsKey(sessionId))
            SessionSpeeds[sessionId] = speed;

        await Clients.Group($"backtest-{sessionId}")
            .SendAsync("PlaybackStateChanged", new { SessionId = sessionId, Speed = speed });

        logger.LogInformation("Session {SessionId} speed changed to x{Speed}", sessionId, speed);
    }

    /// <summary>
    /// Switch the display timeframe. The playback timestamp is preserved —
    /// the chart will re-aggregate M1 data to the new timeframe.
    /// </summary>
    public async Task SetTimeframe(int sessionId, string timeframe)
    {
        if (!Enum.TryParse<Timeframe>(timeframe, ignoreCase: true, out Timeframe tf))
        {
            await Clients.Caller.SendAsync("Error", new { Message = $"Invalid timeframe: {timeframe}" });
            return;
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        IPlaybackEngine engine = scope.ServiceProvider.GetRequiredService<IPlaybackEngine>();
        await engine.ChangeTimeframeAsync(sessionId, tf);

        await Clients.Group($"backtest-{sessionId}")
            .SendAsync("PlaybackStateChanged", new
            {
                SessionId = sessionId,
                Timeframe = tf.ToString()
            });

        logger.LogInformation("Session {SessionId} timeframe changed to {Timeframe}", sessionId, tf);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // Stop any auto-advance loops this connection started so they don't keep
        // advancing candles and hammering the DB after the client is gone.
        foreach ((int sessionId, string connectionId) in SessionPlayers)
        {
            if (connectionId == Context.ConnectionId)
            {
                StopPlaying(sessionId);
                logger.LogInformation(
                    "Stopped playback for session {SessionId} on disconnect of connection {ConnectionId}",
                    sessionId,
                    Context.ConnectionId);
            }
        }

        return base.OnDisconnectedAsync(exception);
    }

    // ─── Private helpers ──────────────────────────────────────

    private async Task AutoAdvanceLoop(int sessionId, CancellationToken ct)
    {
        logger.LogInformation("Auto-advance started for session {SessionId}", sessionId);

        // Read the persisted speed once; SetSpeed updates the cached value live thereafter.
        SessionSpeeds[sessionId] = await LoadPlaybackSpeed(sessionId, ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                bool isEnded = await AdvanceAndNotify(sessionId, ct);

                if (isEnded)
                {
                    StopPlaying(sessionId);
                    break;
                }

                await Task.Delay(GetDelayMs(sessionId), ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation (pause)
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in auto-advance loop for session {SessionId}", sessionId);

            await Clients.Group($"backtest-{sessionId}")
                .SendAsync("Error", new { Message = "Playback error: " + ex.Message });
        }

        logger.LogInformation("Auto-advance stopped for session {SessionId}", sessionId);
    }

    /// <returns>true if session ended</returns>
    private async Task<bool> AdvanceAndNotify(int sessionId, CancellationToken ct = default)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IPlaybackEngine engine = scope.ServiceProvider.GetRequiredService<IPlaybackEngine>();

        PlaybackAdvanceResult result = await engine.AdvanceCandleAsync(sessionId, ct);

        // Build response DTO
        CandleDto? candle = result.Candle is not null
            ? new CandleDto(
                result.Candle.Timestamp,
                result.Candle.Open,
                result.Candle.High,
                result.Candle.Low,
                result.Candle.Close,
                result.Candle.Volume)
            : null;

        // Map filled and closed orders for the notification
        IBacktestDbContext dbContext = scope.ServiceProvider.GetRequiredService<IBacktestDbContext>();

        List<object> filledOrders = result.MatchingResult?.Fills
            .Select(f => (object)new { f.OrderId, f.FilledPrice, f.FilledAt })
            .ToList() ?? [];

        List<object> closedPositions = result.MatchingResult?.Closes
            .Select(c => (object)new { c.OrderId, c.ExitPrice, c.Pnl, c.Reason, c.ClosedAt })
            .ToList() ?? [];

        await Clients.Group($"backtest-{sessionId}")
            .SendAsync("CandleAdvanced", new
            {
                SessionId = sessionId,
                Candle = candle,
                Balance = result.UpdatedBalance,
                Equity = result.MatchingResult?.Equity ?? result.UpdatedBalance,
                UnrealizedPnl = result.MatchingResult?.UnrealizedPnl ?? 0m,
                Timestamp = result.NewTimestamp,
                IsEnded = result.IsSessionEnded,
                IsLiquidated = result.MatchingResult?.IsLiquidated ?? false,
                FilledOrders = filledOrders,
                ClosedPositions = closedPositions
            }, ct);

        return result.IsSessionEnded;
    }

    /// <summary>
    /// Reads the persisted playback speed for a session once (loop startup).
    /// </summary>
    private async Task<int> LoadPlaybackSpeed(int sessionId, CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IBacktestDbContext db = scope.ServiceProvider.GetRequiredService<IBacktestDbContext>();

        int speed = await db.BacktestSessions
            .Where(s => s.Id == sessionId)
            .Select(s => s.PlaybackSpeed)
            .FirstOrDefaultAsync(ct);

        return speed > 0 ? speed : 1;
    }

    /// <summary>
    /// Delay between auto-advance ticks from the cached speed — no scope or DB round trip.
    /// x1 = 1000ms, x2 = 500ms, x5 = 200ms, x10 = 100ms.
    /// </summary>
    private static int GetDelayMs(int sessionId)
    {
        int speed = SessionSpeeds.TryGetValue(sessionId, out int cached) && cached > 0 ? cached : 1;
        return BaseDelayMs / speed;
    }

    private static void StopPlaying(int sessionId)
    {
        SessionPlayers.TryRemove(sessionId, out _);
        SessionSpeeds.TryRemove(sessionId, out _);

        if (PlayingSessions.TryRemove(sessionId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
