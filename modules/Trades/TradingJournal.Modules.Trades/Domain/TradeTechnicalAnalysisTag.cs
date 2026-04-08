using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradeTechnicalAnalysisTags", Schema = "Trades")]
public sealed class TradeTechnicalAnalysisTag : EntityBase<int>
{
    public int TradeHistoryId { get; set; }

    public int TechnicalAnalysisId { get; set; }

    [ForeignKey(nameof(TradeHistoryId))]
    public TradeHistory? TradeHistory { get; set; }

    [ForeignKey(nameof(TechnicalAnalysisId))]
    public TechnicalAnalysis? TechnicalAnalysis { get; set; }
}
