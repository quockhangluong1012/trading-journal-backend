using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Notifications.Domain;

[Table("Notifications", Schema = "Notification")]
[Index(nameof(UserId), nameof(IsRead), nameof(CreatedDate))]
[Index(nameof(UserId), nameof(CreatedDate), Name = "IX_Notifications_UserDate")]
public sealed class Notification : EntityBase<int>
{
    public int UserId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    public NotificationPriority Priority { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// JSON payload for rich notification data (e.g., asset, timeframe, detected pattern details).
    /// </summary>
    [MaxLength(4000)]
    public string? Metadata { get; set; }

    /// <summary>
    /// Optional deep-link URL for the frontend to navigate to on click.
    /// </summary>
    [MaxLength(500)]
    public string? ActionUrl { get; set; }
}
