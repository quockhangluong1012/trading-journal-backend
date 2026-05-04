using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Psychology.Domain;

/// <summary>
/// Records a specific achievement/badge that a user has unlocked.
/// Each achievement can only be unlocked once per user.
/// </summary>
[Table(name: "Achievements", Schema = "Psychology")]
public sealed class Achievement : EntityBase<int>
{
    /// <summary>
    /// The type of achievement that was unlocked.
    /// </summary>
    public AchievementType AchievementType { get; set; }

    /// <summary>
    /// Timestamp when this achievement was unlocked (UTC).
    /// </summary>
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}
