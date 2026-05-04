using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Psychology.Domain;

/// <summary>
/// Records a point-in-time tilt score for a user.
/// A new snapshot is created each time a trade is closed or a tilt recalculation is triggered.
/// </summary>
[Table(name: "TiltSnapshots", Schema = "Psychology")]
public sealed class TiltSnapshot : EntityBase<int>
{
    /// <summary>
    /// Tilt score from 0 (calm) to 100 (max tilt).
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Current consecutive loss streak at the time of this snapshot.
    /// </summary>
    public int ConsecutiveLosses { get; set; }

    /// <summary>
    /// Current consecutive win streak at the time of this snapshot.
    /// </summary>
    public int ConsecutiveWins { get; set; }

    /// <summary>
    /// Number of trades executed in the last 60 minutes (frequency spike detection).
    /// </summary>
    public int TradesLastHour { get; set; }

    /// <summary>
    /// Number of rule breaks in the last 24 hours.
    /// </summary>
    public int RuleBreaksToday { get; set; }

    /// <summary>
    /// Cumulative PnL for today at the time of this snapshot.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TodayPnl { get; set; }

    /// <summary>
    /// The tilt level classification derived from the score.
    /// </summary>
    public TiltLevel Level { get; set; }

    /// <summary>
    /// Whether a circuit breaker notification was fired for this snapshot.
    /// </summary>
    public bool CircuitBreakerTriggered { get; set; }

    /// <summary>
    /// Optional suggested cooldown end time (UTC) when circuit breaker fires.
    /// </summary>
    public DateTime? CooldownUntil { get; set; }

    /// <summary>
    /// Timestamp when this snapshot was taken (UTC).
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
