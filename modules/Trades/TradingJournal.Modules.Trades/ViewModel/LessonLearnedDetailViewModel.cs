namespace TradingJournal.Modules.Trades.ViewModel;

public class LessonLearnedDetailViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public LessonCategory Category { get; set; }

    public LessonSeverity Severity { get; set; }

    public LessonStatus Status { get; set; }

    public List<string> Tags { get; set; } = [];

    public string? KeyTakeaway { get; set; }

    public string? ActionItems { get; set; }

    public int ImpactScore { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public List<LinkedTradeViewModel> LinkedTrades { get; set; } = [];
}

public class LinkedTradeViewModel
{
    public int Id { get; set; }

    public string Asset { get; set; } = string.Empty;

    public PositionType Position { get; set; }

    public decimal EntryPrice { get; set; }

    public decimal? ExitPrice { get; set; }

    public decimal? Pnl { get; set; }

    public string? TradingResult { get; set; }

    public DateTime Date { get; set; }

    public bool IsRuleBroken { get; set; }
}
