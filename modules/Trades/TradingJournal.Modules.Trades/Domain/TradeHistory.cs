using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradeHistories", Schema = "Trades")]
public sealed class TradeHistory : EntityBase<int>
{
    #region Basic Information

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

    public int? TradingSummaryId { get; set; }

    // London / NY / Sydney / Tokyo
    public int? TradingZoneId { get; set; }

    #endregion

    #region Risk Management & Guardrails

    public double TargetTier1 { get; set; }

    public double? TargetTier2 { get; set; }

    public double? TargetTier3 { get; set; }

    public double StopLoss { get; set; }

    #endregion

    #region Psychology & Emotions

    public ConfidenceLevel ConfidenceLevel { get; set; }

    #endregion

    public ICollection<TradeScreenShot> TradeScreenShots { get; set; } = [];

    public ICollection<TradeEmotionTag>? TradeEmotionTags { get; set; } = [];

    public ICollection<TradeHistoryChecklist> TradeChecklists { get; set; } = [];

    public ICollection<TradeTechnicalAnalysisTag> TradeTechnicalAnalysisTags { get; set; } = [];

    [ForeignKey(nameof(TradingSessionId))]
    public TradingSession? TradingSession { get; set; }

    [ForeignKey(nameof(TradingSummaryId))]
    public TradingSummary? TradingSummary { get; set; }

    [ForeignKey(nameof(TradingZoneId))]
    public TradingZone TradingZone { get; set; }
}
