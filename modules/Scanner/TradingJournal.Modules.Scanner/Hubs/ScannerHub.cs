using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.Scanner.Hubs;

/// <summary>
/// SignalR hub for real-time scanner updates.
///
/// Server → Client events:
///   - ScannerAlertDetected: { Alert details }
///   - ScannerStatusChanged: { Status, LastScanTime }
///   - ScanCycleCompleted: { AlertsFound, Duration, Timestamp }
///
/// Users are automatically added to their personal group "user-{userId}" on connection.
/// </summary>
[Authorize]
public sealed class ScannerHub(ILogger<ScannerHub> logger) : Hub
{
    public override async Task OnConnectedAsync()
    {
        int userId = Context.User!.GetCurrentUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        logger.LogDebug("User {UserId} connected to scanner hub (connection={ConnectionId})",
            userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        int userId = Context.User!.GetCurrentUserId();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        logger.LogDebug("User {UserId} disconnected from scanner hub (connection={ConnectionId})",
            userId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
