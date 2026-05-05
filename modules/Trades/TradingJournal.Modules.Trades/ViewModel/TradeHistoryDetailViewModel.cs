namespace TradingJournal.Modules.Trades.ViewModel;

public sealed class TradeHistoryDetailViewModel
{
    public string Asset { get; set; } = string.Empty;

    public PositionType Position { get; set; }

    public decimal EntryPrice { get; set; }

    public DateTime Date { get; set; }

    public TradeStatus Status { get; set; }

    public decimal? ExitPrice { get; set; }

    public decimal? Pnl { get; set; }

    public DateTime? ClosedDate { get; set; }

    public string? TradingResult { get; set; }

    public bool? HitStopLoss { get; set; }

    public string? Notes { get; set; } = string.Empty;

    public int? TradingSessionId { get; set; }

    // London / NY / Sydney / Tokyo
    public int? TradingZoneId { get; set; }

    public decimal TargetTier1 { get; set; }

    public decimal? TargetTier2 { get; set; }

    public decimal? TargetTier3 { get; set; }

    public decimal StopLoss { get; set; }

    public ConfidenceLevel ConfidenceLevel { get; set; }

    public List<string> ScreenShots { get; set; } = [];

    public List<int>? EmotionTags { get; set; } = [];

    public List<int> SelectedChecklists { get; set; } = [];

    public List<int> TechnicalAnalysisTags { get; set; } = [];

    public TradeSummaryViewModel? TradeSummary { get; set; } = new();

    public string? AiSummary { get; set; }

    // ICT Methodology Fields
    public PowerOf3Phase? PowerOf3Phase { get; set; }
    public DailyBias? DailyBias { get; set; }
    public MarketStructure? MarketStructure { get; set; }
    public PremiumDiscount? PremiumDiscount { get; set; }
}