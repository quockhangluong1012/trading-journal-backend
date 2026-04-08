namespace TradingJournal.Modules.Trades.ViewModel;

public sealed class TradeHistoryDetailViewModel
{
    public string Asset { get; set; } = string.Empty;

    public PositionType Position { get; set; }

    public double EntryPrice { get; set; }

    public DateTime Date { get; set; }

    public TradeStatus Status { get; set; }

    public double? ExitPrice { get; set; }

    public double? Pnl { get; set; }

    public DateTime? ClosedDate { get; set; }

    public string? TradingResult { get; set; }

    public bool? HitStopLoss { get; set; }

    public string? Notes { get; set; } = string.Empty;

    public int? TradingSessionId { get; set; }

    // London / NY / Sydney / Tokyo
    public int? TradingZoneId { get; set; }

    public double TargetTier1 { get; set; }

    public double? TargetTier2 { get; set; }

    public double? TargetTier3 { get; set; }

    public double StopLoss { get; set; }

    public ConfidenceLevel ConfidenceLevel { get; set; }

    public List<string> ScreenShots { get; set; } = [];

    public List<int>? EmotionTags { get; set; } = [];

    public List<int> SelectedChecklists { get; set; } = [];

    public List<int> TechnicalAnalysisTags { get; set; } = [];

    public TradeSumamryViewModel? TradeSumamry { get; set; } = new();
}