using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.AiInsights.Domain;

[Table(name: "TradingReviews", Schema = "Trades")]
public sealed class TradingReview : EntityBase<int>
{
    public ReviewPeriodType PeriodType { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public string? UserNotes { get; set; }

    public string? AiSummary { get; set; }

    public string? AiStrengths { get; set; }

    public string? AiWeaknesses { get; set; }

    public string? AiActionItems { get; set; }

    public string? AiTechnicalInsights { get; set; }

    public string? AiPsychologyAnalysis { get; set; }

    public string? AiCriticalMistakesTechnical { get; set; }

    public string? AiCriticalMistakesPsychological { get; set; }

    public string? AiWhatToImprove { get; set; }

    public bool AiSummaryGenerating { get; set; }

    public decimal TotalPnl { get; set; }

    public decimal WinRate { get; set; }

    public int TotalTrades { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }
}
