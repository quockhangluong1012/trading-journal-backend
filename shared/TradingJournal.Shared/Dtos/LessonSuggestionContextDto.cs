namespace TradingJournal.Shared.Dtos;

public sealed class LessonSuggestionContextDto
{
    public int SampleSize { get; set; }

    public string RangeSummary { get; set; } = string.Empty;

    public List<LessonSuggestionTradeDto> Trades { get; set; } = [];

    public List<ExistingLessonContextDto> ExistingLessons { get; set; } = [];
}

public sealed class LessonSuggestionTradeDto
{
    public int TradeId { get; set; }

    public string Asset { get; set; } = string.Empty;

    public string Position { get; set; } = string.Empty;

    public decimal Pnl { get; set; }

    public DateTime ClosedDate { get; set; }

    public bool IsRuleBroken { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string TradingZone { get; set; } = string.Empty;

    public List<string> EmotionTags { get; set; } = [];

    public List<string> TechnicalThemes { get; set; } = [];
}

public sealed class ExistingLessonContextDto
{
    public int LessonId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int Category { get; set; }

    public string? KeyTakeaway { get; set; }

    public List<int> LinkedTradeIds { get; set; } = [];
}