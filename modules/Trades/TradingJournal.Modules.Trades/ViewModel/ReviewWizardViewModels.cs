namespace TradingJournal.Modules.Trades.ViewModel;

public sealed class TradingReviewViewModel
{
    public int Id { get; set; }
    public int PeriodType { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public int Status { get; set; }
    public DateTimeOffset? CompletedDate { get; set; }
    public int? ExecutionRating { get; set; }
    public int? DisciplineRating { get; set; }
    public int? PsychologyRating { get; set; }
    public int? RiskManagementRating { get; set; }
    public int? OverallRating { get; set; }
    public string? PerformanceNotes { get; set; }
    public string? BestTradeReflection { get; set; }
    public string? WorstTradeReflection { get; set; }
    public string? DisciplineNotes { get; set; }
    public string? PsychologyNotes { get; set; }
    public string? GoalsForNextPeriod { get; set; }
    public string? KeyTakeaways { get; set; }
    public int TotalTrades { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal TotalPnl { get; set; }
    public decimal WinRate { get; set; }
    public int RuleBreaks { get; set; }
    public List<ReviewActionItemViewModel> ActionItems { get; set; } = [];
}

public sealed class ReviewActionItemViewModel
{
    public int Id { get; set; }
    public int TradingReviewId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Priority { get; set; }
    public int Status { get; set; }
    public int Category { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset? CompletedDate { get; set; }
    public string? CompletionNotes { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}

public sealed class ReviewWizardDataViewModel
{
    /// <summary>
    /// Current period review snapshot metrics.
    /// </summary>
    public WizardPeriodMetrics Current { get; set; } = new();

    /// <summary>
    /// Previous period review snapshot for comparison.
    /// </summary>
    public WizardPeriodMetrics? Previous { get; set; }

    /// <summary>
    /// Top 3 best trades by PnL in the current period.
    /// </summary>
    public List<WizardTradeHighlight> BestTrades { get; set; } = [];

    /// <summary>
    /// Top 3 worst trades by PnL in the current period.
    /// </summary>
    public List<WizardTradeHighlight> WorstTrades { get; set; } = [];

    /// <summary>
    /// Emotion distribution for the period.
    /// </summary>
    public List<WizardDistributionItem> EmotionDistribution { get; set; } = [];

    /// <summary>
    /// Confidence distribution for the period.
    /// </summary>
    public List<WizardDistributionItem> ConfidenceDistribution { get; set; } = [];

    /// <summary>
    /// Rule compliance summary.
    /// </summary>
    public WizardDisciplineSummary Discipline { get; set; } = new();

    /// <summary>
    /// Open action items from previous reviews.
    /// </summary>
    public List<ReviewActionItemViewModel> PendingActionItems { get; set; } = [];

    /// <summary>
    /// Existing wizard review if one exists for this period.
    /// </summary>
    public TradingReviewViewModel? ExistingReview { get; set; }

    /// <summary>
    /// Consecutive weekly/monthly reviews completed.
    /// </summary>
    public int ReviewStreak { get; set; }
}

public sealed class WizardPeriodMetrics
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public int TotalTrades { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal TotalPnl { get; set; }
    public decimal WinRate { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal BestTradePnl { get; set; }
    public decimal WorstTradePnl { get; set; }
    public int LongTrades { get; set; }
    public int ShortTrades { get; set; }
    public int RuleBreakTrades { get; set; }
    public int HighConfidenceTrades { get; set; }
    public string? TopAsset { get; set; }
    public string? PrimaryTradingZone { get; set; }
    public string? DominantEmotion { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal Expectancy { get; set; }
}

public sealed class WizardTradeHighlight
{
    public int TradeId { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public decimal Pnl { get; set; }
    public DateTimeOffset OpenDate { get; set; }
    public DateTimeOffset ClosedDate { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal? ExitPrice { get; set; }
    public bool IsRuleBroken { get; set; }
    public string? Notes { get; set; }
    public List<string> EmotionTags { get; set; } = [];
}

public sealed class WizardDistributionItem
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class WizardDisciplineSummary
{
    public int TotalRuleChecks { get; set; }
    public int RulesFollowed { get; set; }
    public int RulesBroken { get; set; }
    public decimal ComplianceRate { get; set; }
    public List<WizardRuleBreakdown> RuleBreakdowns { get; set; } = [];
}

public sealed class WizardRuleBreakdown
{
    public string RuleName { get; set; } = string.Empty;
    public int TimesFollowed { get; set; }
    public int TimesBroken { get; set; }
}

public sealed class ReviewStreakViewModel
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalReviews { get; set; }
    public DateTimeOffset? LastReviewDate { get; set; }
}
