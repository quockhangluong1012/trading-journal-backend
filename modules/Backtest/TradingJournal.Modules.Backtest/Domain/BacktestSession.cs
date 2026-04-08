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
    /// Indicates whether historical market data has been downloaded and is ready for playback.
    /// </summary>
    public bool IsDataReady { get; set; }

    // Navigation properties
    public ICollection<BacktestOrder> Orders { get; set; } = [];

    public ICollection<BacktestTradeResult> TradeResults { get; set; } = [];
}
