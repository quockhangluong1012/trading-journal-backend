namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Represents the result of evaluating a single candle against all pending orders and active positions.
/// </summary>
public record MatchingResult(
    List<OrderFill> Fills,
    List<OrderClose> Closes,
    decimal UnrealizedPnl,
    decimal Equity,
    bool IsLiquidated
);

/// <summary>
/// A pending limit order that was filled during candle evaluation.
/// </summary>
public record OrderFill(int OrderId, decimal FilledPrice, DateTime FilledAt);

/// <summary>
/// An active position that was closed during candle evaluation (SL/TP hit).
/// Slippage records the difference between the actual execution price and the requested SL/TP level.
/// Negative slippage = worse for the trader; positive = beneficial.
/// </summary>
public record OrderClose(int OrderId, decimal ExitPrice, decimal Pnl, string Reason, DateTime ClosedAt, decimal Slippage = 0m);

public interface IOrderMatchingEngine
{
    /// <summary>
    /// Evaluates a new OHLCV candle against all pending limit orders and active positions.
    /// Returns fills, closes, unrealized PnL, equity, and liquidation status.
    ///
    /// The candle's OHLC values represent BID prices.
    /// ASK prices are derived as: Ask = Bid + spread.
    /// </summary>
    /// <param name="candle">Current OHLCV candle (BID prices).</param>
    /// <param name="pendingOrders">Pending limit orders to evaluate for fill.</param>
    /// <param name="activePositions">Active positions to evaluate for SL/TP.</param>
    /// <param name="currentBalance">Current account balance.</param>
    /// <param name="spread">Bid/Ask spread in absolute price units.</param>
    MatchingResult EvaluateCandle(
        OhlcvCandle candle,
        List<BacktestOrder> pendingOrders,
        List<BacktestOrder> activePositions,
        decimal currentBalance,
        decimal spread);
}
