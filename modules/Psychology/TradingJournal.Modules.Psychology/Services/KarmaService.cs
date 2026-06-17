using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Psychology.Events;
using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Services;

/// <summary>
/// Manages karma points, levels, and achievement unlocking.
/// </summary>
public interface IKarmaService
{
    /// <summary>
    /// Awards karma points for a specific action and checks for new achievements.
    /// </summary>
    Task<KarmaRecord> AwardKarmaAsync(int userId, KarmaActionType actionType, string description,
        int? referenceId = null, int? overridePoints = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the karma summary for a user (total points, level, title, recent events).
    /// </summary>
    Task<KarmaSummaryViewModel> GetKarmaSummaryAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets karma event history for a user within a date range.
    /// </summary>
    Task<List<KarmaEventViewModel>> GetKarmaHistoryAsync(int userId, int days = 30, CancellationToken ct = default);

    /// <summary>
    /// Gets all achievements with their unlock status for a user.
    /// </summary>
    Task<List<AchievementViewModel>> GetAchievementsAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Recalculates karma from trade data (full rebuild from activity history).
    /// </summary>
    Task<KarmaSummaryViewModel> RecalculateKarmaAsync(int userId, CancellationToken ct = default);
}

internal sealed partial class KarmaService(
    IPsychologyDbContext psychologyDb,
    ITradeProvider tradeProvider,
    IEventBus eventBus,
    ILogger<KarmaService> logger) : IKarmaService
{
    // ── Karma Point Values ──────────────────────────────────────────────

    private static readonly Dictionary<KarmaActionType, int> DefaultPoints = new()
    {
        [KarmaActionType.TradeJournaled] = 5,
        [KarmaActionType.TradeReviewed] = 10,
        [KarmaActionType.PsychologyJournalEntry] = 8,
        [KarmaActionType.DailyJournalingStreak] = 15,
        [KarmaActionType.WeeklyReviewCompleted] = 25,
        [KarmaActionType.WinStreakBonus] = 5,  // Multiplied by streak length
        [KarmaActionType.RuleBrokenPenalty] = -10,
        [KarmaActionType.TiltRecovery] = 20,
        [KarmaActionType.DailyNoteWritten] = 6,
        [KarmaActionType.GoalTaskCompleted] = 10,
        [KarmaActionType.GoalMilestoneCompleted] = 25,
        [KarmaActionType.GoalCompleted] = 50,
        [KarmaActionType.SystemAdjustment] = 0,
    };

    // ── Karma Levels (25 tiers) ─────────────────────────────────────────

    private static readonly (int Threshold, string Title)[] KarmaLevels =
    [
        (0, "Novice Trader"),       // 1
        (100, "Apprentice"),         // 2
        (300, "Journeyman"),         // 3
        (650, "Skilled Trader"),     // 4
        (1100, "Expert"),            // 5
        (1800, "Master Trader"),     // 6
        (2800, "Grandmaster"),       // 7
        (4200, "Elite"),             // 8
        (6000, "Legend"),            // 9
        (8500, "Trading Sage"),      // 10
        (12000, "Warlord"),          // 11
        (16000, "Champion"),         // 12
        (21000, "Conqueror"),        // 13
        (27000, "Titan"),            // 14
        (34000, "Overlord"),         // 15
        (42000, "Sovereign"),        // 16
        (51000, "Ascendant"),        // 17
        (62000, "Celestial"),        // 18
        (75000, "Transcendent"),     // 19
        (90000, "Mythical"),         // 20
        (110000, "Immortal"),        // 21
        (135000, "Demigod"),         // 22
        (165000, "Divine"),          // 23
        (205000, "Eternal"),         // 24
        (250000, "Trading God"),     // 25
    ];

}
