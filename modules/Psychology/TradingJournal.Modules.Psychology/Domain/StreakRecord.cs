using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Psychology.Domain;

/// <summary>
/// Records a point-in-time streak snapshot for a user.
/// A new record is created each time a streak changes (i.e., a trade outcome breaks or extends a streak).
/// </summary>
[Table(name: "StreakRecords", Schema = "Psychology")]
public sealed class StreakRecord : EntityBase<int>
{
    /// <summary>
    /// Type of the current streak: Win, Loss, or None.
    /// </summary>
    public StreakType StreakType { get; set; }

    /// <summary>
    /// Length of the current streak (number of consecutive wins or losses).
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Cumulative PnL accumulated during this streak.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal StreakPnl { get; set; }

    /// <summary>
    /// The best (longest) winning streak the user has ever achieved, at the time of this snapshot.
    /// </summary>
    public int BestWinStreak { get; set; }

    /// <summary>
    /// The worst (longest) losing streak the user has ever experienced, at the time of this snapshot.
    /// </summary>
    public int WorstLossStreak { get; set; }

    /// <summary>
    /// Total number of closed trades at the time of this snapshot.
    /// </summary>
    public int TotalClosedTrades { get; set; }

    /// <summary>
    /// Timestamp when this snapshot was taken (UTC).
    /// </summary>
    public DateTimeOffset RecordedAt { get; set; } = DateTimeOffset.UtcNow;
}
