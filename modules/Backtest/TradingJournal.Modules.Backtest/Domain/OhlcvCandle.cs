using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Backtest.Domain;

// The (Asset, Timeframe, Timestamp) unique index is configured fluently in
// BacktestDbContext to keep a single source of truth and avoid duplicate indexes.
[Table("OhlcvCandles", Schema = "Backtest")]
public sealed class OhlcvCandle : EntityBase<int>
{
    [MaxLength(20)]
    public string Asset { get; set; } = string.Empty;

    public Timeframe Timeframe { get; set; }

    public DateTime Timestamp { get; set; }

    [Column(TypeName = "decimal(28,10)")]
    public decimal Open { get; set; }

    [Column(TypeName = "decimal(28,10)")]
    public decimal High { get; set; }

    [Column(TypeName = "decimal(28,10)")]
    public decimal Low { get; set; }

    [Column(TypeName = "decimal(28,10)")]
    public decimal Close { get; set; }

    [Column(TypeName = "decimal(28,10)")]
    public decimal Volume { get; set; }
}
