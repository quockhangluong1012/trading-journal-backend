using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradingReviews", Schema = "Trades")]
public sealed class TradingReview : EntityBase<int>
{
    public ReviewPeriodType PeriodType { get; set; }

    public DateTimeOffset PeriodStart { get; set; }

    public DateTimeOffset PeriodEnd { get; set; }

    public ReviewWizardStatus Status { get; set; } = ReviewWizardStatus.Draft;

    public DateTimeOffset? CompletedDate { get; set; }

    #region Step Ratings (1-5 scale)

    /// <summary>
    /// Self-rating: How well did I execute my setups? (1-5)
    /// </summary>
    public int? ExecutionRating { get; set; }

    /// <summary>
    /// Self-rating: How disciplined was I with rules? (1-5)
    /// </summary>
    public int? DisciplineRating { get; set; }

    /// <summary>
    /// Self-rating: How well did I manage emotions? (1-5)
    /// </summary>
    public int? PsychologyRating { get; set; }

    /// <summary>
    /// Self-rating: How effective was my risk management? (1-5)
    /// </summary>
    public int? RiskManagementRating { get; set; }

    /// <summary>
    /// Overall self-rating for the period (1-5)
    /// </summary>
    public int? OverallRating { get; set; }

    #endregion

    #region Step Notes

    [MaxLength(2000)]
    public string? PerformanceNotes { get; set; }

    [MaxLength(2000)]
    public string? BestTradeReflection { get; set; }

    [MaxLength(2000)]
    public string? WorstTradeReflection { get; set; }

    [MaxLength(2000)]
    public string? DisciplineNotes { get; set; }

    [MaxLength(2000)]
    public string? PsychologyNotes { get; set; }

    [MaxLength(2000)]
    public string? GoalsForNextPeriod { get; set; }

    [MaxLength(2000)]
    public string? KeyTakeaways { get; set; }

    #endregion

    #region Snapshot Metrics (captured at completion)

    public int TotalTrades { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPnl { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WinRate { get; set; }

    public int RuleBreaks { get; set; }

    #endregion

    public ICollection<ReviewActionItem> ActionItems { get; set; } = [];
}
