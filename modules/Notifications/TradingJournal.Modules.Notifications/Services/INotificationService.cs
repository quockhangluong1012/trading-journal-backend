namespace TradingJournal.Modules.Notifications.Services;

public interface INotificationService
{
    /// <summary>
    /// Creates a notification in the database and pushes it to the user via SignalR in real-time.
    /// Returns the notification ID.
    /// </summary>
    Task<int> CreateAndPushAsync(
        int userId,
        string title,
        string message,
        NotificationType type,
        NotificationPriority priority,
        string? metadata = null,
        string? actionUrl = null,
        CancellationToken ct = default);
}
