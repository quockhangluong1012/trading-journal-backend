using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "LessonTradeLinks", Schema = "Trades")]
public sealed class LessonTradeLink : EntityBase<int>
{
    public int LessonLearnedId { get; set; }

    public int TradeHistoryId { get; set; }

    [ForeignKey(nameof(LessonLearnedId))]
    public LessonLearned LessonLearned { get; set; } = null!;

    [ForeignKey(nameof(TradeHistoryId))]
    public TradeHistory TradeHistory { get; set; } = null!;
}
