using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Backtest.Domain;

[Table("BacktestOrders", Schema = "Backtest")]
public sealed class BacktestOrder : EntityBase<int>
{
    public int SessionId { get; set; }

    public BacktestOrderType OrderType { get; set; }

    public BacktestOrderSide Side { get; set; }

    public BacktestOrderStatus Status { get; set; }

    /// <summary>
    /// For Market orders: the execution price (close of current candle).
    /// For Limit orders: the target entry price.
    /// </summary>
    public decimal EntryPrice { get; set; }

    /// <summary>
    /// Actual price the order was filled at (for limit orders, may differ slightly due to gap simulation).
    /// </summary>
    public decimal? FilledPrice { get; set; }

    public decimal PositionSize { get; set; }

    public decimal? StopLoss { get; set; }

    public decimal? TakeProfit { get; set; }

    public decimal? ExitPrice { get; set; }

    public decimal? Pnl { get; set; }

    /// <summary>
    /// Simulated timestamp when the order was placed.
    /// </summary>
    public DateTime OrderedAt { get; set; }

    /// <summary>
    /// Simulated timestamp when the order was filled (limit order triggered or market order executed).
    /// </summary>
    public DateTime? FilledAt { get; set; }

    /// <summary>
    /// Simulated timestamp when the position was closed (SL, TP, manual, or liquidation).
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    [ForeignKey(nameof(SessionId))]
    public BacktestSession Session { get; set; } = null!;
}
