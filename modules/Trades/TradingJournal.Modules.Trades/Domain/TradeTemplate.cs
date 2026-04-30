using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradeTemplates", Schema = "Trades")]
public sealed class TradeTemplate : EntityBase<int>
{
    /// <summary>
    /// User-friendly name for the template, e.g. "EURUSD London OB Long".
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of when to use this template.
    /// </summary>
    public string? Description { get; set; }

    #region Pre-filled Trade Fields

    /// <summary>
    /// Default asset symbol, e.g. "EURUSD", "BTCUSD".
    /// </summary>
    public string? Asset { get; set; }

    /// <summary>
    /// Default position direction.
    /// </summary>
    public PositionType? Position { get; set; }

    /// <summary>
    /// Default trading zone (London / NY / Sydney / Tokyo).
    /// </summary>
    public int? TradingZoneId { get; set; }

    /// <summary>
    /// Default trading session FK.
    /// </summary>
    public int? TradingSessionId { get; set; }

    /// <summary>
    /// Default trading setup FK (cross-module, Setups schema).
    /// </summary>
    public int? TradingSetupId { get; set; }

    /// <summary>
    /// Default stop loss distance (in pips or price units).
    /// </summary>
    public decimal? DefaultStopLoss { get; set; }

    /// <summary>
    /// Default target tier 1.
    /// </summary>
    public decimal? DefaultTargetTier1 { get; set; }

    /// <summary>
    /// Default target tier 2.
    /// </summary>
    public decimal? DefaultTargetTier2 { get; set; }

    /// <summary>
    /// Default target tier 3.
    /// </summary>
    public decimal? DefaultTargetTier3 { get; set; }

    /// <summary>
    /// Default confidence level for this template.
    /// </summary>
    public ConfidenceLevel? DefaultConfidenceLevel { get; set; }

    /// <summary>
    /// Default notes template text.
    /// </summary>
    public string? DefaultNotes { get; set; }

    /// <summary>
    /// Comma-separated list of default pretrade checklist IDs.
    /// </summary>
    public string? DefaultChecklistIds { get; set; }

    /// <summary>
    /// Comma-separated list of default emotion tag IDs.
    /// </summary>
    public string? DefaultEmotionTagIds { get; set; }

    /// <summary>
    /// Comma-separated list of default technical analysis tag IDs.
    /// </summary>
    public string? DefaultTechnicalAnalysisTagIds { get; set; }

    #endregion

    /// <summary>
    /// Number of times this template has been used.
    /// </summary>
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// Whether this template is pinned/favorited for quick access.
    /// </summary>
    public bool IsFavorite { get; set; } = false;

    /// <summary>
    /// Display order for sorting templates.
    /// </summary>
    public int SortOrder { get; set; } = 0;

    [ForeignKey(nameof(TradingZoneId))]
    public TradingZone? TradingZone { get; set; }

    [ForeignKey(nameof(TradingSessionId))]
    public TradingSession? TradingSession { get; set; }
}
