using Microsoft.Extensions.Logging;

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
    ILogger<PlaybackEngine> logger) : IPlaybackEngine
{
    public async Task<PlaybackAdvanceResult> AdvanceCandleAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        BacktestSession session = await context.BacktestSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        if (session.Status != BacktestSessionStatus.InProgress)
        {
            return new PlaybackAdvanceResult(null, null, session.CurrentBalance, session.CurrentTimestamp, true);
        }

        // ── Fetch the next DISPLAY candle (the one shown on screen) ──
        OhlcvCandle? displayCandle = await aggregationService.GetNextAggregatedCandleAsync(
            session.Asset,
            session.ActiveTimeframe,
            session.CurrentTimestamp,
            cancellationToken);

        if (displayCandle is null)
        {
            session.Status = BacktestSessionStatus.Completed;
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Session {SessionId} completed — no more candles.", sessionId);
            return new PlaybackAdvanceResult(null, null, session.CurrentBalance, session.CurrentTimestamp, true);
        }

        // Check end date boundary
        if (session.EndDate.HasValue && displayCandle.Timestamp > session.EndDate.Value)
        {
            session.Status = BacktestSessionStatus.Completed;
            await context.SaveChangesAsync(cancellationToken);
            return new PlaybackAdvanceResult(null, null, session.CurrentBalance, session.CurrentTimestamp, true);
        }

        // ── Load orders ──
        List<BacktestOrder> pendingOrders = await context.BacktestOrders
            .Where(o => o.SessionId == sessionId && o.Status == BacktestOrderStatus.Pending)
            .ToListAsync(cancellationToken);

        List<BacktestOrder> activePositions = await context.BacktestOrders
            .Where(o => o.SessionId == sessionId && o.Status == BacktestOrderStatus.Active)
            .ToListAsync(cancellationToken);

        // ── INTRA-BAR M1 EVALUATION ──
        // If display timeframe > M1 AND there are pending/active orders,
        // iterate through underlying M1 candles for accurate SL/TP resolution.
        MatchingResult result;
        bool hasOrders = pendingOrders.Count > 0 || activePositions.Count > 0;

        if (session.ActiveTimeframe != Timeframe.M1 && hasOrders)
        {
            result = await EvaluateIntraBarAsync(
                session, displayCandle, pendingOrders, activePositions, cancellationToken);
        }
        else
        {
            // No orders to evaluate, or already on M1 — use the display candle directly
            result = matchingEngine.EvaluateCandle(
                displayCandle, pendingOrders, activePositions, session.CurrentBalance, session.Spread);
        }

        // ── Persist fills ──
        foreach (OrderFill fill in result.Fills)
        {
            BacktestOrder? order = await context.BacktestOrders.FindAsync([fill.OrderId], cancellationToken);
            if (order is null) continue;

            order.Status = BacktestOrderStatus.Active;
            order.FilledPrice = fill.FilledPrice;
            order.FilledAt = fill.FilledAt;
        }

        // ── Persist closes ──
        foreach (OrderClose close in result.Closes)
        {
            BacktestOrder? order = await context.BacktestOrders.FindAsync([close.OrderId], cancellationToken);
            if (order is null) continue;

            order.Status = BacktestOrderStatus.Closed;
            order.ExitPrice = close.ExitPrice;
            order.Pnl = close.Pnl;
            order.ClosedAt = close.ClosedAt;

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
                BalanceAfter = session.CurrentBalance + close.Pnl,
                EntryTime = order.FilledAt ?? order.OrderedAt,
                ExitTime = close.ClosedAt,
                ExitReason = close.Reason
            }, cancellationToken);
        }

        // ── Update session state ──
        decimal newBalance = session.CurrentBalance + result.Closes.Sum(c => c.Pnl);
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
            result.IsLiquidated || session.Status == BacktestSessionStatus.Completed);
    }

    /// <summary>
    /// Iterates through all M1 candles within the display candle's time period
    /// to accurately determine the real order of SL/TP hits.
    ///
    /// Example: D1 candle 2024-01-15
    ///   → loads M1 candles from 2024-01-15 00:00 to 2024-01-15 23:59
    ///   → evaluates each M1 candle against pending/active orders
    ///   → stops at the first SL/TP hit (accurate price movement simulation)
    /// </summary>
    private async Task<MatchingResult> EvaluateIntraBarAsync(
        BacktestSession session,
        OhlcvCandle displayCandle,
        List<BacktestOrder> pendingOrders,
        List<BacktestOrder> activePositions,
        CancellationToken cancellationToken)
    {
        int bucketMinutes = (int)session.ActiveTimeframe;
        DateTime periodStart = displayCandle.Timestamp;
        DateTime periodEnd = periodStart.AddMinutes(bucketMinutes);

        // Load all M1 candles within this display candle's period
        List<OhlcvCandle> m1Candles = await context.OhlcvCandles
            .Where(c => c.Asset == session.Asset && c.Timeframe == Timeframe.M1
                        && c.Timestamp >= periodStart && c.Timestamp < periodEnd)
            .OrderBy(c => c.Timestamp)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (m1Candles.Count == 0)
        {
            // Fallback: no M1 data available, use the display candle directly
            logger.LogWarning(
                "No M1 data for intra-bar evaluation of {Asset} at {Timestamp}. Falling back to display candle.",
                session.Asset, displayCandle.Timestamp);
            return matchingEngine.EvaluateCandle(
                displayCandle, pendingOrders, activePositions, session.CurrentBalance, session.Spread);
        }

        logger.LogDebug(
            "Intra-bar evaluation: {M1Count} M1 candles for {Timeframe} candle at {Timestamp}",
            m1Candles.Count, session.ActiveTimeframe, displayCandle.Timestamp);

        // Iterate through each M1 candle chronologically
        // Accumulate all fills/closes across the M1 candles
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
                m1Candle, pendingOrders, activePositions, balance, session.Spread);

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
}
