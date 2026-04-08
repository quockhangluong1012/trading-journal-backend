using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Backtest.Dto;
using TradingJournal.Modules.Backtest.Services;

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
        // Clean up any playing sessions for this connection
        // Note: In production, track which connection started which session
        return base.OnDisconnectedAsync(exception);
    }

    // ─── Private helpers ──────────────────────────────────────

    private async Task AutoAdvanceLoop(int sessionId, CancellationToken ct)
    {
        logger.LogInformation("Auto-advance started for session {SessionId}", sessionId);

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

                // Get current speed from DB
                int delayMs = await GetDelayMs(sessionId, ct);
                await Task.Delay(delayMs, ct);
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

    private async Task<int> GetDelayMs(int sessionId, CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        IBacktestDbContext db = scope.ServiceProvider.GetRequiredService<IBacktestDbContext>();

        int speed = await db.BacktestSessions
            .Where(s => s.Id == sessionId)
            .Select(s => s.PlaybackSpeed)
            .FirstOrDefaultAsync(ct);

        if (speed <= 0) speed = 1;

        // x1 = 1000ms, x2 = 500ms, x5 = 200ms, x10 = 100ms
        return BaseDelayMs / speed;
    }

    private static void StopPlaying(int sessionId)
    {
        if (PlayingSessions.TryRemove(sessionId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
