using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Notifications.Dto;
using TradingJournal.Modules.Notifications.Hubs;

namespace TradingJournal.Modules.Notifications.Services;

internal sealed class NotificationService(
    INotificationDbContext context,
    IHubContext<NotificationHub> hubContext,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<int> CreateAndPushAsync(
        int userId,
        string title,
        string message,
        NotificationType type,
        NotificationPriority priority,
        string? metadata = null,
        string? actionUrl = null,
        CancellationToken ct = default)
    {
        // 1. Persist the notification
        var notification = new Notification
        {
            Id = default!,
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Priority = priority,
            Metadata = metadata,
            ActionUrl = actionUrl,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = userId
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync(ct);

        // 2. Build the DTO for the SignalR push
        var dto = new NotificationDto(
            notification.Id,
            notification.Title,
            notification.Message,
            notification.Type.ToString(),
            notification.Priority.ToString(),
            notification.IsRead,
            notification.ReadAt,
            notification.Metadata,
            notification.ActionUrl,
            notification.CreatedDate);

        // 3. Push to the user's personal group via SignalR
        string groupName = $"user-{userId}";

        await hubContext.Clients.Group(groupName)
            .SendAsync("NewNotification", dto, ct);

        // 4. Push updated unread count
        int unreadCount = await context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDisabled, ct);

        await hubContext.Clients.Group(groupName)
            .SendAsync("UnreadCountChanged", new UnreadCountDto(unreadCount), ct);

        logger.LogDebug(
            "Notification {NotificationId} created and pushed to user {UserId} (type={Type}, priority={Priority})",
            notification.Id, userId, type, priority);

        return notification.Id;
    }
}
