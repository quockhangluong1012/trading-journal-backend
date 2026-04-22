using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradeEmotionTags", Schema = "Trades")]
public sealed class TradeEmotionTag : EntityBase<int>
{
    public int TradeHistoryId { get; set; }

    public int EmotionTagId { get; set; }

    [ForeignKey(nameof(TradeHistoryId))]
    public TradeHistory TradeHistory { get; set; }
}
