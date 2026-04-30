using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.RiskManagement.Domain;

[Table(name: "DailyRiskSnapshots", Schema = "Risk")]
public sealed class DailyRiskSnapshot : EntityBase<int>
{
    /// <summary>
    /// The date this snapshot represents.
    /// </summary>
    public DateTime SnapshotDate { get; set; }

    /// <summary>
    /// Total PnL for this day.
    /// </summary>
    public decimal DailyPnl { get; set; }

    /// <summary>
    /// Cumulative PnL for the current week (Mon-Fri).
    /// </summary>
    public decimal WeeklyPnl { get; set; }

    /// <summary>
    /// Number of trades taken this day.
    /// </summary>
    public int TradesTaken { get; set; }

    /// <summary>
    /// Number of wins this day.
    /// </summary>
    public int Wins { get; set; }

    /// <summary>
    /// Number of losses this day.
    /// </summary>
    public int Losses { get; set; }

    /// <summary>
    /// Whether the daily loss limit was breached.
    /// </summary>
    public bool DailyLimitBreached { get; set; }

    /// <summary>
    /// Whether the weekly drawdown cap was breached.
    /// </summary>
    public bool WeeklyCapBreached { get; set; }

    /// <summary>
    /// Account balance at end of day.
    /// </summary>
    public decimal AccountBalanceEod { get; set; }

    /// <summary>
    /// Maximum drawdown percentage for this day.
    /// </summary>
    public decimal MaxDrawdownPercent { get; set; }
}
