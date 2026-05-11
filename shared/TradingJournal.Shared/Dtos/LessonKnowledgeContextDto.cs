namespace TradingJournal.Shared.Dtos;

public sealed class LessonKnowledgeContextDto
{
    public string FocusQuery { get; set; } = string.Empty;

    public List<LessonKnowledgeItemDto> Lessons { get; set; } = [];
}

public sealed class LessonKnowledgeItemDto
{
    public int LessonId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public string Content { get; set; } = string.Empty;

    public string? KeyTakeaway { get; set; }

    public string? ActionItems { get; set; }

    public int ImpactScore { get; set; }

    public List<int> LinkedTradeIds { get; set; } = [];
}