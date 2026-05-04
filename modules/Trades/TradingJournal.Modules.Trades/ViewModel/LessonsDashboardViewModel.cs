namespace TradingJournal.Modules.Trades.ViewModel;

public class LessonsDashboardViewModel
{
    // Lesson stats
    public int TotalLessons { get; set; }
    public int ActiveLessons { get; set; }
    public int AppliedLessons { get; set; }
    public int CriticalLessons { get; set; }

    // Category breakdown
    public List<CategoryBreakdownItem> CategoryBreakdown { get; set; } = [];

    // Discipline stats (from DisciplineLog)
    public decimal DisciplineScore { get; set; }
    public decimal DisciplineScoreTrend { get; set; }
    public int TotalRulesChecked { get; set; }
    public int TotalRulesFollowed { get; set; }
    public int TotalRulesBroken { get; set; }

    // Discipline over time (for chart)
    public List<DisciplineTimePoint> DisciplineTimeline { get; set; } = [];

    // Recent lessons
    public List<LessonLearnedViewModel> RecentLessons { get; set; } = [];

    // Most impactful lessons
    public List<LessonLearnedViewModel> TopImpactLessons { get; set; } = [];

    // Loss trade stats
    public decimal TotalLossFromLinkedTrades { get; set; }
    public int LinkedTradesCount { get; set; }
}

public class CategoryBreakdownItem
{
    public LessonCategory Category { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class DisciplineTimePoint
{
    public DateTimeOffset Date { get; set; }
    public decimal Score { get; set; }
    public int TotalChecks { get; set; }
    public int Followed { get; set; }
}
