using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Contracts;

namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Orchestrates playback advancement with INTRA-BAR M1 evaluation.
///
/// KEY DESIGN: When advancing one display candle (e.g., D1), the engine internally
/// iterates through all underlying M1 candles within that period to accurately
/// determine the ORDER of SL/TP hits. This prevents look-ahead bias.
///
/// Example: A D1 candle with long upper/lower wicks hitting both SL and TP.
/// Without intra-bar evaluation, we can't know which was hit first.
/// With M1 evaluation, we replay 1440 M1 candles to find the exact sequence.
///
/// Supports:
///   - Skip (advance 1 display candle)
///   - Play/Pause (auto-advance via BacktestHub)
///   - Speed control (x1, x2, x5, x10 — controls delay between advances)
///   - Multi-timeframe sync (change display timeframe without losing position)
/// </summary>
internal sealed class PlaybackEngine(
    IBacktestDbContext context,
    IOrderMatchingEngine matchingEngine,
    ICandleAggregationService aggregationService,
    IEventBus eventBus,
    IBacktestSessionLock sessionLock,
    ILogger<PlaybackEngine> logger) : IPlaybackEngine
{
    public async Task<PlaybackAdvanceResult> AdvanceCandleAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        // Serialize against manual close / finish so the read-modify-write of CurrentBalance
        // below can't interleave with another path mutating the same session.
        await using IAsyncDisposable _ = await sessionLock.AcquireAsync(sessionId, cancellationToken);

        BacktestSession session = await context.BacktestSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        if (session.Status != BacktestSessionStatus.InProgress)
        {
            return new PlaybackAdvanceResult(null, null, session.CurrentBalance, session.CurrentTimestamp, true);
        }

        // ── Load orders ──
        List<BacktestOrder> pendingOrders = await context.BacktestOrders
            .Where(o => o.SessionId == sessionId && o.Status == BacktestOrderStatus.Pending)
            .ToListAsync(cancellationToken);

        List<BacktestOrder> activePositions = await context.BacktestOrders
            .Where(o => o.SessionId == sessionId && o.Status == BacktestOrderStatus.Active)
            .ToListAsync(cancellationToken);

        // The matcher reports fills/closes by order id. Those orders are exactly the
        // tracked entities we just loaded above (intra-bar replay shuffles them between
        // the two lists but keeps the same instances), so build one id→entity lookup now
        // and reuse it to persist mutations — no FindAsync round trip per order.
        Dictionary<int, BacktestOrder> ordersById = pendingOrders
            .Concat(activePositions)
            .ToDictionary(o => o.Id);

        // ── INTRA-BAR M1 EVALUATION ──
        // If display timeframe > M1 AND there are pending/active orders, we must replay
        // the underlying M1 candles for accurate SL/TP resolution. That same M1 window
        // also defines the display candle's O/H/L/C/V — so load it ONCE and reuse it for
        // both, rather than reading the bucket once to aggregate and again to replay.
        // With no orders (or on M1) there's nothing to replay, so the display candle is
        // computed with a SQL aggregate that never materializes the bucket's M1 rows.
        bool hasOrders = pendingOrders.Count > 0 || activePositions.Count > 0;
        bool intraBar = session.ActiveTimeframe != Timeframe.M1 && hasOrders;

        OhlcvCandle? displayCandle;
        List<OhlcvCandle>? bucketM1Candles = null;

        if (intraBar)
        {
            NextBucket? bucket = await aggregationService.GetNextBucketWithM1Async(
                session.Asset, session.ActiveTimeframe, session.CurrentTimestamp, cancellationToken);
            displayCandle = bucket?.DisplayCandle;
            bucketM1Candles = bucket?.M1Candles;
        }
        else
        {
            displayCandle = await aggregationService.GetNextAggregatedCandleAsync(
                session.Asset, session.ActiveTimeframe, session.CurrentTimestamp, cancellationToken);
        }

        if (displayCandle is null)
        {
            session.Status = BacktestSessionStatus.Completed;
            session.EndDate = session.CurrentTimestamp;
            await context.SaveChangesAsync(cancellationToken);
            await PublishCompletionAsync(session, cancellationToken);

            logger.LogInformation("Session {SessionId} completed — no more candles.", sessionId);
            return new PlaybackAdvanceResult(null, null, session.CurrentBalance, session.CurrentTimestamp, true);
        }

        // Check end date boundary
        if (session.EndDate.HasValue && displayCandle.Timestamp > session.EndDate.Value)
        {
            session.Status = BacktestSessionStatus.Completed;
            session.EndDate ??= session.CurrentTimestamp;
            await context.SaveChangesAsync(cancellationToken);
            await PublishCompletionAsync(session, cancellationToken);
            return new PlaybackAdvanceResult(null, null, session.CurrentBalance, session.CurrentTimestamp, true);
        }

        MatchingResult result = intraBar
            ? EvaluateIntraBar(session, bucketM1Candles!, pendingOrders, activePositions)
            : matchingEngine.EvaluateCandle(
                displayCandle, pendingOrders, activePositions, session.CurrentBalance, session.Spread, session.Leverage, session.MaintenanceMarginPercentage);

        // ── Persist fills ──
        List<BacktestOrder> filledOrders = [];
        foreach (OrderFill fill in result.Fills)
        {
            if (!ordersById.TryGetValue(fill.OrderId, out BacktestOrder? order)) continue;

            order.Status = BacktestOrderStatus.Active;
            order.FilledPrice = fill.FilledPrice;
            order.FilledAt = fill.FilledAt;
            filledOrders.Add(order);
        }

        // ── Persist closes ──
        // Thread a running balance through the closes so each trade result records the
        // cumulative BalanceAfter. result.Closes is in chronological evaluation order
        // (intra-bar appends per M1 step; single-bar appends SL/TP then liquidation), so
        // folding PnL in order reproduces the true equity progression. Computing it as
        // "session balance + this close's PnL" would give every simultaneous close the
        // same pre-advance baseline and corrupt the equity curve / drawdown analytics.
        decimal runningBalance = session.CurrentBalance;
        List<BacktestOrder> closedOrders = [];
        foreach (OrderClose close in result.Closes)
        {
            if (!ordersById.TryGetValue(close.OrderId, out BacktestOrder? order)) continue;

            order.Status = BacktestOrderStatus.Closed;
            order.ExitPrice = close.ExitPrice;
            order.Pnl = close.Pnl;
            order.ClosedAt = close.ClosedAt;
            closedOrders.Add(order);

            runningBalance += close.Pnl;

            await context.BacktestTradeResults.AddAsync(new BacktestTradeResult
            {
                Id = 0,
                SessionId = sessionId,
                OrderId = close.OrderId,
                Side = order.Side,
                EntryPrice = order.FilledPrice ?? order.EntryPrice,
                ExitPrice = close.ExitPrice,
                PositionSize = order.PositionSize,
                Pnl = close.Pnl,
                BalanceAfter = runningBalance,
                EntryTime = order.FilledAt ?? order.OrderedAt,
                ExitTime = close.ClosedAt,
                ExitReason = close.Reason
            }, cancellationToken);
        }

        // ── Update session state ──
        decimal newBalance = runningBalance;
        session.CurrentBalance = newBalance;
        session.CurrentTimestamp = displayCandle.Timestamp;

        if (result.IsLiquidated)
        {
            session.Status = BacktestSessionStatus.Liquidated;

            List<BacktestOrder> remainingPending = await context.BacktestOrders
                .Where(o => o.SessionId == sessionId && o.Status == BacktestOrderStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (BacktestOrder pending in remainingPending)
            {
                pending.Status = BacktestOrderStatus.Cancelled;
            }

            logger.LogWarning("Session {SessionId} LIQUIDATED at {Timestamp}", sessionId, displayCandle.Timestamp);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new PlaybackAdvanceResult(
            displayCandle,
            result,
            newBalance,
            displayCandle.Timestamp,
            result.IsLiquidated || session.Status == BacktestSessionStatus.Completed,
            filledOrders,
            closedOrders);
    }

    /// <summary>
    /// Iterates through all M1 candles within the display candle's time period
    /// to accurately determine the real order of SL/TP hits.
    ///
    /// The M1 window is passed in already loaded — it was read once by the caller and
    /// reused both to derive the display candle and to drive this replay, avoiding a
    /// second read of the same bucket on the hot auto-play path.
    ///
    /// Example: D1 candle 2024-01-15
    ///   → replays M1 candles from 2024-01-15 00:00 to 2024-01-15 23:59
    ///   → evaluates each M1 candle against pending/active orders
    ///   → stops at the first SL/TP hit (accurate price movement simulation)
    /// </summary>
    private MatchingResult EvaluateIntraBar(
        BacktestSession session,
        List<OhlcvCandle> m1Candles,
        List<BacktestOrder> pendingOrders,
        List<BacktestOrder> activePositions)
    {
        List<OrderFill> allFills = [];
        List<OrderClose> allCloses = [];
        decimal balance = session.CurrentBalance;
        decimal unrealizedPnl = 0m;
        decimal equity = balance;
        bool isLiquidated = false;

        foreach (OhlcvCandle m1Candle in m1Candles)
        {
            if (isLiquidated) break;

            // Only evaluate if there are still pending/active orders
            if (pendingOrders.Count == 0 && activePositions.Count == 0)
                break;

            MatchingResult m1Result = matchingEngine.EvaluateCandle(
                m1Candle, pendingOrders, activePositions, balance, session.Spread, session.Leverage, session.MaintenanceMarginPercentage);

            // Collect results
            allFills.AddRange(m1Result.Fills);
            allCloses.AddRange(m1Result.Closes);

            // Update running balance
            balance += m1Result.Closes.Sum(c => c.Pnl);
            unrealizedPnl = m1Result.UnrealizedPnl;
            equity = m1Result.Equity;
            isLiquidated = m1Result.IsLiquidated;

            // Remove closed orders from active positions (they are already handled)
            HashSet<int> closedOrderIds = m1Result.Closes.Select(c => c.OrderId).ToHashSet();
            activePositions.RemoveAll(p => closedOrderIds.Contains(p.Id));

            // Move filled orders from pending to active (so next M1 candles evaluate their TP/SL)
            HashSet<int> filledOrderIds = m1Result.Fills.Select(f => f.OrderId).ToHashSet();
            var newlyFilled = pendingOrders.Where(p => filledOrderIds.Contains(p.Id)).ToList();
            pendingOrders.RemoveAll(p => filledOrderIds.Contains(p.Id));

            foreach (var filled in newlyFilled)
            {
                var fillData = m1Result.Fills.First(f => f.OrderId == filled.Id);
                filled.FilledPrice = fillData.FilledPrice;
                filled.FilledAt = fillData.FilledAt;
                activePositions.Add(filled);
            }
        }

        return new MatchingResult(allFills, allCloses, unrealizedPnl, equity, isLiquidated);
    }

    public async Task UpdatePlaybackSpeedAsync(int sessionId, int speed, CancellationToken cancellationToken = default)
    {
        BacktestSession session = await context.BacktestSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        session.PlaybackSpeed = speed;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeTimeframeAsync(int sessionId, Timeframe newTimeframe, CancellationToken cancellationToken = default)
    {
        BacktestSession session = await context.BacktestSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        session.ActiveTimeframe = newTimeframe;
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Session {SessionId} timeframe changed to {Timeframe}. Timestamp preserved at {Timestamp}.",
            sessionId, newTimeframe, session.CurrentTimestamp);
    }

    private async Task PublishCompletionAsync(BacktestSession session, CancellationToken cancellationToken)
    {
        List<BacktestTradeResult> results = await context.BacktestTradeResults
            .AsNoTracking()
            .Where(result => result.SessionId == session.Id)
            .ToListAsync(cancellationToken);

        await eventBus.PublishAsync(new BacktestSessionCompletedEvent(
            Guid.NewGuid(),
            session.CreatedBy,
            session.Id,
            session.EndDate ?? session.CurrentTimestamp,
            results.Count,
            results.Count(result => result.Pnl > 0m),
            session.CurrentBalance - session.InitialBalance), cancellationToken);
    }
}
