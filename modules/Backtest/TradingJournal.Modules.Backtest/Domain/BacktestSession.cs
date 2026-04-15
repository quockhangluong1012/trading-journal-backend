using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Backtest.Domain;

[Table("BacktestSessions", Schema = "Backtest")]
public sealed class BacktestSession : EntityBase<int>
{
    public string Asset { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal InitialBalance { get; set; }

    public decimal CurrentBalance { get; set; }

    public BacktestSessionStatus Status { get; set; } = BacktestSessionStatus.InProgress;

    /// <summary>
    /// The simulated timestamp the playback has advanced to.
    /// </summary>
    public DateTime CurrentTimestamp { get; set; }

    public Timeframe ActiveTimeframe { get; set; } = Timeframe.M15;

    public int PlaybackSpeed { get; set; } = 1;

    /// <summary>
    /// Leverage applied to trades in this session (e.g., 50 means 50:1).
    /// </summary>
    public int Leverage { get; set; } = 50;

    /// <summary>
    /// The percentage of margin that must be maintained. Liquidation triggers if Equity drops below Maintenance Margin (Trade Value / Leverage * Percentage).
    /// E.g. 0.50 means 50%.
    /// </summary>
    [Column(TypeName = "decimal(5,4)")]
    public decimal MaintenanceMarginPercentage { get; set; } = 0.50m;

    /// <summary>
    /// Spread in absolute price units applied during simulation.
    /// Calculated at session creation: DefaultSpreadPips * PipSize.
    /// OHLC data represents BID prices; Ask = Bid + Spread.
    /// </summary>
    [Column(TypeName = "decimal(28,10)")]
    public decimal Spread { get; set; }

    /// <summary>
    /// Indicates whether historical market data has been downloaded and is ready for playback.
    /// </summary>
    public bool IsDataReady { get; set; }

    // Navigation properties
    public ICollection<BacktestOrder> Orders { get; set; } = [];

    public ICollection<BacktestTradeResult> TradeResults { get; set; } = [];
}
