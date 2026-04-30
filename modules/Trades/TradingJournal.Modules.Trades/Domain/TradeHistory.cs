using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradeHistories", Schema = "Trades")]
public sealed class TradeHistory : EntityBase<int>
{
    #region Basic Information

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

    public int? TradingSummaryId { get; set; }

    // London / NY / Sydney / Tokyo
    public int? TradingZoneId { get; set; }

    /// <summary>
    /// FK to the TradingSetup used for this trade (cross-module, Setups schema).
    /// Nullable because older trades may not have a setup assigned.
    /// </summary>
    public int? TradingSetupId { get; set; }

    #endregion

    #region Risk Management & Guardrails

    public decimal TargetTier1 { get; set; }

    public decimal? TargetTier2 { get; set; }

    public decimal? TargetTier3 { get; set; }

    public decimal StopLoss { get; set; }

    public bool IsRuleBroken { get; set; } = false;

    public string? RuleBreakReason { get; set; }

    #endregion

    #region Psychology & Emotions
    public ConfidenceLevel ConfidenceLevel { get; set; }

    #endregion

    public string? AiSummary { get; set; }

    public ICollection<TradeScreenShot> TradeScreenShots { get; set; } = [];

    public ICollection<TradeEmotionTag>? TradeEmotionTags { get; set; } = [];

    public ICollection<TradeHistoryChecklist> TradeChecklists { get; set; } = [];

    public ICollection<TradeTechnicalAnalysisTag> TradeTechnicalAnalysisTags { get; set; } = [];

    [ForeignKey(nameof(TradingSessionId))]
    public TradingSession? TradingSession { get; set; }

    [ForeignKey(nameof(TradingZoneId))]
    public TradingZone TradingZone { get; set; }
}
