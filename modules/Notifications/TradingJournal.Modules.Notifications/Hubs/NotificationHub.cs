using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.Notifications.Hubs;

/// <summary>
/// SignalR hub for real-time notification delivery.
///
/// Server → Client events:
///   - NewNotification: { Id, Title, Message, Type, Priority, Metadata, ActionUrl, CreatedDate }
///   - NotificationRead: { Id }
///   - UnreadCountChanged: { Count }
///
/// Users are automatically added to their personal group "user-{userId}" on connection.
/// </summary>
[Authorize]
public sealed class NotificationHub(ILogger<NotificationHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        int userId = Context.User!.GetCurrentUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        logger.LogDebug("User {UserId} connected to notification hub (connection={ConnectionId})",
            userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        int userId = Context.User!.GetCurrentUserId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        logger.LogDebug("User {UserId} disconnected from notification hub (connection={ConnectionId})",
            userId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
