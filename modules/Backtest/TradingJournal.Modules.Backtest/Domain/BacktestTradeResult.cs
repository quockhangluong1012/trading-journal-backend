using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Backtest.Domain;

[Table("BacktestTradeResults", Schema = "Backtest")]
public sealed class BacktestTradeResult : EntityBase<int>
{
    public int SessionId { get; set; }

    public int OrderId { get; set; }

    public BacktestOrderSide Side { get; set; }

    public decimal EntryPrice { get; set; }

    public decimal ExitPrice { get; set; }

    public decimal PositionSize { get; set; }

    public decimal Pnl { get; set; }

    /// <summary>
    /// Account balance after this trade was closed.
    /// </summary>
    public decimal BalanceAfter { get; set; }

    public DateTime EntryTime { get; set; }

    public DateTime ExitTime { get; set; }

    /// <summary>
    /// Reason the trade was closed: "TP Hit", "SL Hit", "Manual", "Liquidated".
    /// </summary>
    public string ExitReason { get; set; } = string.Empty;

    [ForeignKey(nameof(SessionId))]
    public BacktestSession Session { get; set; } = null!;

    [ForeignKey(nameof(OrderId))]
    public BacktestOrder Order { get; set; } = null!;
}
