namespace TradingJournal.Shared.Dtos;

public sealed class ResearchKnowledgeContextDto
{
    public string FocusQuery { get; set; } = string.Empty;

    public List<LessonKnowledgeItemDto> Lessons { get; set; } = [];

    public List<PlaybookKnowledgeItemDto> Playbooks { get; set; } = [];

    public List<DailyNoteKnowledgeItemDto> DailyNotes { get; set; } = [];
}

public sealed class PlaybookKnowledgeItemDto
{
    public int SetupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? EntryRules { get; set; }

    public string? ExitRules { get; set; }

    public string? IdealMarketConditions { get; set; }

    public decimal? RiskPerTrade { get; set; }

    public decimal? TargetRiskReward { get; set; }

    public string? PreferredTimeframes { get; set; }

    public string? PreferredAssets { get; set; }
}

public sealed class DailyNoteKnowledgeItemDto
{
    public int DailyNoteId { get; set; }

    public DateOnly NoteDate { get; set; }

    public string DailyBias { get; set; } = string.Empty;

    public string MarketStructureNotes { get; set; } = string.Empty;

    public string KeyLevelsAndLiquidity { get; set; } = string.Empty;

    public string NewsAndEvents { get; set; } = string.Empty;

    public string SessionFocus { get; set; } = string.Empty;

    public string RiskAppetite { get; set; } = string.Empty;

    public string MentalState { get; set; } = string.Empty;

    public string KeyRulesAndReminders { get; set; } = string.Empty;
}