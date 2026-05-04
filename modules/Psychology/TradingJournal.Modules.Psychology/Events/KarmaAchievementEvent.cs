using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Modules.Psychology.Events;

/// <summary>
/// Integration event published when a user unlocks a new achievement.
/// Consumed by the Notification module to push achievement alerts to the user.
/// </summary>
public sealed record KarmaAchievementEvent(
    Guid EventId,
    int UserId,
    string AchievementName,
    string AchievementDescription,
    string Emoji,
    int TotalKarma,
    int KarmaLevel,
    string KarmaTitle) : IntegrationEvent(EventId);
