using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradeHistoryChecklists", Schema = "Trades")]
public sealed class TradeHistoryChecklist : EntityBase<int>
{
    public int TradeHistoryId { get; set; }

    public int PretradeChecklistId { get; set; }

    [ForeignKey(nameof(TradeHistoryId))]
    public TradeHistory TradeHistory { get; set; }

    [ForeignKey(nameof(PretradeChecklistId))]
    public PretradeChecklist PretradeChecklist { get; set; }
}