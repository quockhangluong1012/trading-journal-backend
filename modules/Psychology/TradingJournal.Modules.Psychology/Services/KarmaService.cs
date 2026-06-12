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
        (50, "Apprentice"),          // 2
        (150, "Journeyman"),         // 3
        (300, "Skilled Trader"),     // 4
        (500, "Expert"),             // 5
        (750, "Master Trader"),      // 6
        (1100, "Grandmaster"),       // 7
        (1500, "Elite"),             // 8
        (2000, "Legend"),            // 9
        (3000, "Trading Sage"),      // 10
        (4000, "Warlord"),           // 11
        (5500, "Champion"),          // 12
        (7500, "Conqueror"),         // 13
        (10000, "Titan"),            // 14
        (13000, "Overlord"),         // 15
        (16500, "Sovereign"),        // 16
        (20500, "Ascendant"),        // 17
        (25000, "Celestial"),        // 18
        (30000, "Transcendent"),     // 19
        (36000, "Mythical"),         // 20
        (43000, "Immortal"),         // 21
        (51000, "Demigod"),          // 22
        (60000, "Divine"),           // 23
        (72000, "Eternal"),          // 24
        (85000, "Trading God"),      // 25
    ];

}
