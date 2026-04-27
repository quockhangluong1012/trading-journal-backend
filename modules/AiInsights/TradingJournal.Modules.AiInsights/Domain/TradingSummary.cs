using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.AiInsights.Domain;

[Table(name: "TradingSummaries", Schema = "Trades")]
public sealed class TradingSummary : EntityBase<int>
{
    public int TradeId { get; set; }

    public string ExecutiveSummary { get; set; } = string.Empty;

    public string TechnicalInsights { get; set; } = string.Empty;

    public string PsychologyAnalysis { get; set; } = string.Empty;

    public CriticalMistakes CriticalMistakes { get; set; } = new();
}

public class CriticalMistakes
{
    public List<string> Technical { get; set; } = [];

    public List<string> Psychological { get; set; } = [];
}
