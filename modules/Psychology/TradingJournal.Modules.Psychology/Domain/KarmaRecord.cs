using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Psychology.Domain;

/// <summary>
/// Records a single karma point event — either earned or deducted.
/// Each row represents one action that modified the user's total karma.
/// </summary>
[Table(name: "KarmaRecords", Schema = "Psychology")]
public sealed class KarmaRecord : EntityBase<int>
{
    /// <summary>
    /// The type of action that earned/deducted karma.
    /// </summary>
    public KarmaActionType ActionType { get; set; }

    /// <summary>
    /// The number of points earned (positive) or deducted (negative).
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Human-readable description of why karma was awarded.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional reference to a related entity (e.g., TradeId, JournalId).
    /// </summary>
    public int? ReferenceId { get; set; }

    /// <summary>
    /// Timestamp when this karma event occurred (UTC).
    /// </summary>
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}
