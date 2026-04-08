using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Core matching engine that evaluates each OHLCV candle against pending limit orders
/// and active positions. Handles limit fills, SL/TP hits, and liquidation detection.
///
/// Design decisions:
/// - SL is checked BEFORE TP (worst-case assumption for the trader)
/// - Limit orders fill at their specified entry price (not at candle open)
/// - Liquidation cancels all remaining pending orders
/// - PnL uses (exit - entry) * size for Longs and (entry - exit) * size for Shorts
/// </summary>
internal sealed class OrderMatchingEngine(ILogger<OrderMatchingEngine> logger) : IOrderMatchingEngine
{
    public MatchingResult EvaluateCandle(
        OhlcvCandle candle,
        List<BacktestOrder> pendingOrders,
        List<BacktestOrder> activePositions,
        decimal currentBalance)
    {
        List<OrderFill> fills = [];
        List<OrderClose> closes = [];
        decimal balance = currentBalance;

        // ─────────────────────────────────────────────────
        // 1. Evaluate Pending Limit Orders for fills
        // ─────────────────────────────────────────────────
        foreach (BacktestOrder order in pendingOrders)
        {
            if (IsLimitOrderTriggered(candle, order))
            {
                decimal filledPrice = order.EntryPrice;

                fills.Add(new OrderFill(order.Id, filledPrice, candle.Timestamp));

                // Move this order to the active pool so it's evaluated for SL/TP
                // in the same candle (important for volatile candles)
                order.Status = BacktestOrderStatus.Active;
                order.FilledPrice = filledPrice;
                order.FilledAt = candle.Timestamp;
                activePositions.Add(order);

                logger.LogDebug(
                    "Limit order {OrderId} filled at {Price} on candle {Timestamp}",
                    order.Id, filledPrice, candle.Timestamp);
            }
        }

        // ─────────────────────────────────────────────────
        // 2. Evaluate Active Positions for SL/TP hits
        // ─────────────────────────────────────────────────
        List<BacktestOrder> remainingPositions = [];

        foreach (BacktestOrder position in activePositions)
        {
            OrderClose? closeResult = EvaluatePosition(candle, position);

            if (closeResult is not null)
            {
                closes.Add(closeResult);
                balance += closeResult.Pnl;

                logger.LogDebug(
                    "Position {OrderId} closed: {Reason} at {ExitPrice}, PnL: {Pnl}",
                    position.Id, closeResult.Reason, closeResult.ExitPrice, closeResult.Pnl);
            }
            else
            {
                remainingPositions.Add(position);
            }
        }

        // ─────────────────────────────────────────────────
        // 3. Calculate Unrealized PnL on remaining positions
        // ─────────────────────────────────────────────────
        decimal unrealizedPnl = 0m;

        foreach (BacktestOrder position in remainingPositions)
        {
            unrealizedPnl += CalculateUnrealizedPnl(position, candle.Close);
        }

        decimal equity = balance + unrealizedPnl;

        // ─────────────────────────────────────────────────
        // 4. Liquidation check
        // ─────────────────────────────────────────────────
        bool isLiquidated = equity <= 0m;

        if (isLiquidated)
        {
            logger.LogWarning("LIQUIDATION triggered. Equity: {Equity}, Balance: {Balance}", equity, balance);

            // Force close all remaining positions at current candle close
            foreach (BacktestOrder position in remainingPositions)
            {
                decimal pnl = CalculateRealizedPnl(position, candle.Close);
                closes.Add(new OrderClose(position.Id, candle.Close, pnl, "Liquidated", candle.Timestamp));
                balance += pnl;
            }

            equity = balance;
        }

        return new MatchingResult(fills, closes, unrealizedPnl, equity, isLiquidated);
    }

    /// <summary>
    /// Checks if a limit order should trigger on this candle.
    /// Long Limit: triggers when candle.Low &lt;= entry price (price dipped to our buy level).
    /// Short Limit: triggers when candle.High &gt;= entry price (price rose to our sell level).
    /// </summary>
    private static bool IsLimitOrderTriggered(OhlcvCandle candle, BacktestOrder order)
    {
        if (order.OrderType != BacktestOrderType.Limit || order.Status != BacktestOrderStatus.Pending)
            return false;

        return order.Side switch
        {
            BacktestOrderSide.Long => candle.Low <= order.EntryPrice && candle.High >= order.EntryPrice,
            BacktestOrderSide.Short => candle.High >= order.EntryPrice && candle.Low <= order.EntryPrice,
            _ => false
        };
    }

    /// <summary>
    /// Evaluates an active position against the current candle for SL and TP hits.
    /// SL is checked first (worst-case assumption).
    /// </summary>
    private static OrderClose? EvaluatePosition(OhlcvCandle candle, BacktestOrder position)
    {
        decimal filledPrice = position.FilledPrice ?? position.EntryPrice;

        // Check Stop Loss first (worst-case)
        if (position.StopLoss.HasValue)
        {
            bool slHit = position.Side switch
            {
                BacktestOrderSide.Long => candle.Low <= position.StopLoss.Value,
                BacktestOrderSide.Short => candle.High >= position.StopLoss.Value,
                _ => false
            };

            if (slHit)
            {
                decimal exitPrice = position.StopLoss.Value;
                decimal pnl = CalculateRealizedPnlStatic(position.Side, filledPrice, exitPrice, position.PositionSize);
                return new OrderClose(position.Id, exitPrice, pnl, "SL Hit", candle.Timestamp);
            }
        }

        // Check Take Profit
        if (position.TakeProfit.HasValue)
        {
            bool tpHit = position.Side switch
            {
                BacktestOrderSide.Long => candle.High >= position.TakeProfit.Value,
                BacktestOrderSide.Short => candle.Low <= position.TakeProfit.Value,
                _ => false
            };

            if (tpHit)
            {
                decimal exitPrice = position.TakeProfit.Value;
                decimal pnl = CalculateRealizedPnlStatic(position.Side, filledPrice, exitPrice, position.PositionSize);
                return new OrderClose(position.Id, exitPrice, pnl, "TP Hit", candle.Timestamp);
            }
        }

        return null;
    }

    private static decimal CalculateUnrealizedPnl(BacktestOrder position, decimal currentPrice)
    {
        decimal filledPrice = position.FilledPrice ?? position.EntryPrice;
        return CalculateRealizedPnlStatic(position.Side, filledPrice, currentPrice, position.PositionSize);
    }

    private static decimal CalculateRealizedPnl(BacktestOrder position, decimal exitPrice)
    {
        decimal filledPrice = position.FilledPrice ?? position.EntryPrice;
        return CalculateRealizedPnlStatic(position.Side, filledPrice, exitPrice, position.PositionSize);
    }

    /// <summary>
    /// PnL calculation:
    /// - Long: (exitPrice - entryPrice) * positionSize
    /// - Short: (entryPrice - exitPrice) * positionSize
    /// </summary>
    private static decimal CalculateRealizedPnlStatic(
        BacktestOrderSide side, decimal entryPrice, decimal exitPrice, decimal positionSize)
    {
        return side switch
        {
            BacktestOrderSide.Long => (exitPrice - entryPrice) * positionSize,
            BacktestOrderSide.Short => (entryPrice - exitPrice) * positionSize,
            _ => 0m
        };
    }
}
