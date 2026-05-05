using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Psychology.Domain;

/// <summary>
/// Stores a user's daily trading preparation notes.
/// One record per user per calendar date (UTC).
/// </summary>
[Table(name: "DailyNotes", Schema = "Psychology")]
public sealed class DailyNote : EntityBase<int>
{
    /// <summary>
    /// The calendar date this note applies to (date-only, stored as midnight UTC).
    /// </summary>
    [Column(TypeName = "date")]
    public DateOnly NoteDate { get; set; }

    /// <summary>
    /// Higher-timeframe directional bias: Bullish, Bearish, or Neutral.
    /// </summary>
    [MaxLength(16)]
    public string DailyBias { get; set; } = string.Empty;

    /// <summary>
    /// Market structure observations (e.g. BOS, CHoCH, order flow).
    /// </summary>
    public string MarketStructureNotes { get; set; } = string.Empty;

    /// <summary>
    /// Key price levels, liquidity pools, and fair-value gaps to watch.
    /// </summary>
    public string KeyLevelsAndLiquidity { get; set; } = string.Empty;

    /// <summary>
    /// High-impact news events or economic releases for the day.
    /// </summary>
    public string NewsAndEvents { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated session focus (e.g. "Asian,London,NewYork").
    /// </summary>
    [MaxLength(128)]
    public string SessionFocus { get; set; } = string.Empty;

    /// <summary>
    /// Risk appetite for the day: Conservative, Normal, or Aggressive.
    /// </summary>
    [MaxLength(16)]
    public string RiskAppetite { get; set; } = string.Empty;

    /// <summary>
    /// Pre-market mental state and mindset check-in.
    /// </summary>
    public string MentalState { get; set; } = string.Empty;

    /// <summary>
    /// Personal trading rules and reminders for the day.
    /// </summary>
    public string KeyRulesAndReminders { get; set; } = string.Empty;
}
