namespace TradingJournal.Shared.Dtos;

/// <summary>
/// DTO containing all trade data needed by the AI service for generating trade analysis summaries.
/// Implemented by the Trades module, consumed by AiInsights module.
/// </summary>
public sealed class AiTradeDetailDto
{
    public int TradeHistoryId { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public decimal EntryPrice { get; set; }
    public decimal TargetTier1 { get; set; }
    public decimal? TargetTier2 { get; set; }
    public decimal? TargetTier3 { get; set; }
    public decimal StopLoss { get; set; }
    public string Notes { get; set; } = string.Empty;
    public decimal? ExitPrice { get; set; }
    public decimal? Pnl { get; set; }
    public string ConfidenceLevel { get; set; } = string.Empty;
    public string TradingZone { get; set; } = string.Empty;
    public DateTime OpenDate { get; set; }
    public DateTime ClosedDate { get; set; }
    public List<string> TechnicalAnalysisTags { get; set; } = [];
    public List<string> EmotionTags { get; set; } = [];
    public List<string> ChecklistItems { get; set; } = [];
    public List<string> ScreenshotUrls { get; set; } = [];
    public List<string> PsychologyNotes { get; set; } = [];
}
