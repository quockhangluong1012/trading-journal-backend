using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradeHistories", Schema = "Trades")]
public sealed class TradeHistory : EntityBase<int>
{
    #region Basic Information

    public string Asset { get; set; } = string.Empty;
    
    public PositionType Position { get; set; }

    public decimal EntryPrice { get; set; }

    public DateTimeOffset Date { get; set; }

    public TradeStatus Status { get; set; }

    public decimal? ExitPrice { get; set; }

    public decimal? Pnl { get; set; }

    public DateTimeOffset? ClosedDate { get; set; }

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

    #region ICT Methodology Fields

    /// <summary>
    /// Power of 3 (AMD) phase at time of entry.
    /// </summary>
    public PowerOf3Phase? PowerOf3Phase { get; set; }

    /// <summary>
    /// Daily bias direction based on higher-timeframe ICT analysis.
    /// </summary>
    public DailyBias? DailyBias { get; set; }

    /// <summary>
    /// Market structure condition at entry (BOS, CHoCH, HH, HL, LH, LL).
    /// </summary>
    public MarketStructure? MarketStructure { get; set; }

    /// <summary>
    /// Whether the entry was in Premium, Discount, or Equilibrium zone.
    /// </summary>
    public PremiumDiscount? PremiumDiscount { get; set; }

    #endregion

    public ICollection<TradeScreenShot> TradeScreenShots { get; set; } = [];

    public ICollection<TradeEmotionTag>? TradeEmotionTags { get; set; } = [];

    public ICollection<TradeHistoryChecklist> TradeChecklists { get; set; } = [];

    public ICollection<TradeTechnicalAnalysisTag> TradeTechnicalAnalysisTags { get; set; } = [];

    [ForeignKey(nameof(TradingSessionId))]
    public TradingSession? TradingSession { get; set; }

    [ForeignKey(nameof(TradingZoneId))]
    public TradingZone? TradingZone { get; set; }
}
