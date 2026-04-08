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
/// </summary>
public record OrderClose(int OrderId, decimal ExitPrice, decimal Pnl, string Reason, DateTime ClosedAt);

public interface IOrderMatchingEngine
{
    /// <summary>
    /// Evaluates a new OHLCV candle against all pending limit orders and active positions.
    /// Returns fills, closes, unrealized PnL, equity, and liquidation status.
    /// </summary>
    MatchingResult EvaluateCandle(
        OhlcvCandle candle,
        List<BacktestOrder> pendingOrders,
        List<BacktestOrder> activePositions,
        decimal currentBalance);
}
