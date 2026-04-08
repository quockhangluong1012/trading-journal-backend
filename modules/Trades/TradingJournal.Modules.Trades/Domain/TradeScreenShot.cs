using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradeScreenshots", Schema = "Trades")]
public sealed class TradeScreenShot : EntityBase<int>
{
    public string Url { get; set; } = string.Empty;

    public int TradeHistoryId { get; set; }

    [ForeignKey(nameof(TradeHistoryId))]
    public TradeHistory TradeHistory { get; set; }
}
