namespace TradingJournal.Modules.Trades.ViewModel;

public sealed class TradeTemplateViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Asset { get; set; }

    public int? Position { get; set; }

    public int? TradingZoneId { get; set; }

    public string? TradingZoneName { get; set; }

    public int? TradingSessionId { get; set; }

    public int? TradingSetupId { get; set; }

    public decimal? DefaultStopLoss { get; set; }

    public decimal? DefaultTargetTier1 { get; set; }

    public decimal? DefaultTargetTier2 { get; set; }

    public decimal? DefaultTargetTier3 { get; set; }

    public int? DefaultConfidenceLevel { get; set; }

    public string? DefaultNotes { get; set; }

    public List<int>? DefaultChecklistIds { get; set; }

    public List<int>? DefaultEmotionTagIds { get; set; }

    public List<int>? DefaultTechnicalAnalysisTagIds { get; set; }

    public int UsageCount { get; set; }

    public bool IsFavorite { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedDate { get; set; }
}
