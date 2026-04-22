using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradingSessions", Schema = "Trades")]
public sealed class TradingSession : EntityBase<int>
{
    public DateTime FromTime { get; set; }

    public DateTime? ToTime { get; set; }

    public string? Duration { get; set; } = string.Empty;

    public decimal? PnL { get; set; }

    public string? Note { get; set; } = string.Empty;

    public int TradeCount { get; set; }

    public TradingSessionStatus Status { get; set; } = TradingSessionStatus.Active;
}
