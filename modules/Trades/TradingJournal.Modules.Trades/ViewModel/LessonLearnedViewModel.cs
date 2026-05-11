namespace TradingJournal.Modules.Trades.ViewModel;

public class LessonLearnedViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public LessonCategory Category { get; set; }

    public LessonSeverity Severity { get; set; }

    public LessonStatus Status { get; set; }

    public List<string> Tags { get; set; } = [];

    public string? KeyTakeaway { get; set; }

    public int ImpactScore { get; set; }

    public int LinkedTradesCount { get; set; }

    public DateTime CreatedDate { get; set; }
}
