using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Notifications.Common.Enums;
using TradingJournal.Modules.Notifications.Services;

namespace TradingJournal.Modules.Notifications.EventHandlers;

/// <summary>
/// Handles <see cref="GoalItemCompletedEvent"/> from the Goals module (published
/// only on an item's first completion). Creates a notification and pushes it in
/// real-time via SignalR so the user gets a "🎯 Goal completed" toast and the
/// open goals page can live-update.
/// </summary>
internal sealed class GoalCompletedNotificationHandler(
    INotificationService notificationService,
    ILogger<GoalCompletedNotificationHandler> logger) : INotificationHandler<GoalItemCompletedEvent>
{
    public async Task Handle(GoalItemCompletedEvent notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "🎯 Handling goal completion for user {UserId}: {Kind} #{ItemId} \"{Title}\"",
            notification.UserId, notification.ItemKind, notification.ItemId, notification.Title);

        (string emoji, string label) = Describe(notification.ItemKind);

        string metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            notification.GoalId,
            notification.ItemId,
            ItemKind = notification.ItemKind.ToString(),
        });

        await notificationService.CreateAndPushAsync(
            notification.UserId,
            $"{emoji} {label} completed",
            $"You completed the {label.ToLowerInvariant()} \"{notification.Title}\".",
            NotificationType.GoalCompleted,
            NotificationPriority.Normal,
            metadata,
            $"/goals/{notification.GoalId}",
            cancellationToken);
    }

    private static (string Emoji, string Label) Describe(GoalItemKind itemKind) => itemKind switch
    {
        GoalItemKind.Goal => ("🎯", "Goal"),
        GoalItemKind.Milestone => ("🏁", "Milestone"),
        GoalItemKind.Task => ("✅", "Task"),
        _ => throw new ArgumentOutOfRangeException(nameof(itemKind), itemKind, null),
    };
}
