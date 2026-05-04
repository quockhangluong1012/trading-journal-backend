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

internal sealed class KarmaService(
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

    // ── Achievement Definitions (68 achievements) ──────────────────────

    private static readonly AchievementDefinition[] AchievementDefinitions =
    [
        // ── Trade milestones (12) ──
        new(AchievementType.FirstTrade, "First Blood", "Log your very first trade", "🎯", "Trades"),
        new(AchievementType.TenTrades, "Getting Started", "Log 10 trades", "📊", "Trades"),
        new(AchievementType.TwentyFiveTrades, "Quarter Century", "Log 25 trades", "📈", "Trades"),
        new(AchievementType.FiftyTrades, "Half Century", "Log 50 trades", "📉", "Trades"),
        new(AchievementType.HundredTrades, "Century Trader", "Log 100 trades", "💯", "Trades"),
        new(AchievementType.TwoFiftyTrades, "Seasoned Trader", "Log 250 trades", "🎪", "Trades"),
        new(AchievementType.FiveHundredTrades, "Market Veteran", "Log 500 trades", "⚔️", "Trades"),
        new(AchievementType.ThousandTrades, "Trading Machine", "Log 1,000 trades", "🏆", "Trades"),
        new(AchievementType.TwoThousandFiveHundredTrades, "War Machine", "Log 2,500 trades", "🤖", "Trades"),
        new(AchievementType.FiveThousandTrades, "Trade God", "Log 5,000 trades", "👁️", "Trades"),
        new(AchievementType.SevenThousandFiveHundredTrades, "Market Oracle", "Log 7,500 trades", "🔮", "Trades"),
        new(AchievementType.TenThousandTrades, "Eternal Trader", "Log 10,000 trades", "♾️", "Trades"),

        // ── Review milestones (8) ──
        new(AchievementType.FirstReview, "Self-Aware", "Complete your first trade review", "📝", "Reviews"),
        new(AchievementType.FiveReviews, "Curious Mind", "Complete 5 trade reviews", "🔍", "Reviews"),
        new(AchievementType.TenReviews, "Reflective Mind", "Complete 10 trade reviews", "🪞", "Reviews"),
        new(AchievementType.TwentyFiveReviews, "Analyst", "Complete 25 trade reviews", "🔬", "Reviews"),
        new(AchievementType.FiftyReviews, "Deep Thinker", "Complete 50 trade reviews", "🧐", "Reviews"),
        new(AchievementType.HundredReviews, "Review Oracle", "Complete 100 trade reviews", "📚", "Reviews"),
        new(AchievementType.TwoHundredReviews, "Trade Scientist", "Complete 200 trade reviews", "🔭", "Reviews"),
        new(AchievementType.FiveHundredReviews, "Review Grandmaster", "Complete 500 trade reviews", "🏛️", "Reviews"),

        // ── Journaling streaks (14) ──
        new(AchievementType.ThreeDayStreak, "Warming Up", "3-day journaling streak", "🌱", "Streaks"),
        new(AchievementType.FiveDayStreak, "Building Habit", "5-day journaling streak", "🌿", "Streaks"),
        new(AchievementType.WeekStreak, "Week Warrior", "7-day journaling streak", "🔥", "Streaks"),
        new(AchievementType.TwoWeekStreak, "Fortnight Force", "14-day journaling streak", "💪", "Streaks"),
        new(AchievementType.ThreeWeekStreak, "Consistency King", "21-day journaling streak", "👑", "Streaks"),
        new(AchievementType.MonthStreak, "Monthly Master", "30-day journaling streak", "⚡", "Streaks"),
        new(AchievementType.FortyFiveDayStreak, "Habit Forged", "45-day journaling streak", "🔨", "Streaks"),
        new(AchievementType.SixtyDayStreak, "Two-Month Titan", "60-day journaling streak", "🌊", "Streaks"),
        new(AchievementType.QuarterStreak, "Quarter Legend", "90-day journaling streak", "💎", "Streaks"),
        new(AchievementType.FourMonthStreak, "Relentless", "120-day journaling streak", "🦁", "Streaks"),
        new(AchievementType.HalfYearStreak, "Half-Year Hero", "180-day journaling streak", "🏔️", "Streaks"),
        new(AchievementType.EightMonthStreak, "Marathon Mind", "240-day journaling streak", "🏃", "Streaks"),
        new(AchievementType.TenMonthStreak, "Almost There", "300-day journaling streak", "🎯", "Streaks"),
        new(AchievementType.YearStreak, "Yearly Immortal", "365-day journaling streak", "🌌", "Streaks"),

        // ── Win streaks (8) ──
        new(AchievementType.WinStreak3, "Lucky Run", "3 consecutive winning trades", "🍀", "Performance"),
        new(AchievementType.WinStreak5, "Hot Hand", "5 consecutive winning trades", "🎰", "Performance"),
        new(AchievementType.WinStreak7, "On Fire", "7 consecutive winning trades", "🔥", "Performance"),
        new(AchievementType.WinStreak10, "Unstoppable", "10 consecutive winning trades", "🌟", "Performance"),
        new(AchievementType.WinStreak15, "Legendary Run", "15 consecutive winning trades", "💫", "Performance"),
        new(AchievementType.WinStreak20, "Invincible", "20 consecutive winning trades", "☄️", "Performance"),
        new(AchievementType.WinStreak25, "Mythic Streak", "25 consecutive winning trades", "🐉", "Performance"),
        new(AchievementType.WinStreak30, "Godlike", "30 consecutive winning trades", "🏅", "Performance"),

        // ── Karma levels (10) ──
        new(AchievementType.KarmaLevel2, "First Steps", "Reach karma level 2", "🌱", "Karma"),
        new(AchievementType.KarmaLevel3, "Finding Rhythm", "Reach karma level 3", "🎵", "Karma"),
        new(AchievementType.KarmaLevel5, "Rising Star", "Reach karma level 5", "⭐", "Karma"),
        new(AchievementType.KarmaLevel7, "Gaining Momentum", "Reach karma level 7", "🚀", "Karma"),
        new(AchievementType.KarmaLevel10, "Moonwalker", "Reach karma level 10", "🌙", "Karma"),
        new(AchievementType.KarmaLevel12, "Orbit Breaker", "Reach karma level 12", "🛸", "Karma"),
        new(AchievementType.KarmaLevel15, "Galaxy Brain", "Reach karma level 15", "🌌", "Karma"),
        new(AchievementType.KarmaLevel18, "Nebula Walker", "Reach karma level 18", "🪐", "Karma"),
        new(AchievementType.KarmaLevel20, "Ascended", "Reach karma level 20", "🔱", "Karma"),
        new(AchievementType.KarmaLevel25, "Trading Royalty", "Reach karma level 25", "👑", "Karma"),

        // ── Psychology (16) ──
        new(AchievementType.TiltRecovery1, "First Breath", "Recover from tilt for the first time", "🌬️", "Psychology"),
        new(AchievementType.TiltRecovery3, "Steady Hands", "Recover from tilt 3 times", "🙏", "Psychology"),
        new(AchievementType.TiltMaster, "Zen Master", "Recover from tilt 5 times", "🧘", "Psychology"),
        new(AchievementType.TiltGuru, "Inner Peace", "Recover from tilt 10 times", "☮️", "Psychology"),
        new(AchievementType.TiltRecovery15, "Tilt Slayer", "Recover from tilt 15 times", "⚔️", "Psychology"),
        new(AchievementType.TiltEnlightened, "Enlightened", "Recover from tilt 25 times", "🕊️", "Psychology"),
        new(AchievementType.TiltRecovery50, "Emotion Architect", "Recover from tilt 50 times", "🏛️", "Psychology"),
        new(AchievementType.Disciplined, "Iron Discipline", "20 consecutive trades with no rule breaks", "🎖️", "Psychology"),
        new(AchievementType.Disciplined30, "Steely Resolve", "30 consecutive trades with no rule breaks", "🔩", "Psychology"),
        new(AchievementType.Disciplined50, "Steel Mind", "50 consecutive trades with no rule breaks", "⚙️", "Psychology"),
        new(AchievementType.Disciplined100, "Diamond Hands", "100 consecutive trades with no rule breaks", "💎", "Psychology"),
        new(AchievementType.Disciplined200, "Unbreakable", "200 consecutive trades with no rule breaks", "🛡️", "Psychology"),
        new(AchievementType.Disciplined300, "Fortress", "300 consecutive trades with no rule breaks", "🏰", "Psychology"),
        new(AchievementType.Disciplined500, "Absolute Zero", "500 consecutive trades with no rule breaks", "❄️", "Psychology"),
        new(AchievementType.JournalEntries5, "First Reflections", "Write 5 psychology journal entries", "📓", "Psychology"),
        new(AchievementType.JournalEntries10, "Mind Explorer", "Write 10 psychology journal entries", "🧠", "Psychology"),
        new(AchievementType.JournalEntries25, "Thought Leader", "Write 25 psychology journal entries", "💡", "Psychology"),
        new(AchievementType.JournalEntries50, "Psych Adept", "Write 50 psychology journal entries", "🔮", "Psychology"),
        new(AchievementType.JournalEntries100, "Mental Fortress", "Write 100 psychology journal entries", "🏰", "Psychology"),
        new(AchievementType.JournalEntries250, "Soul Architect", "Write 250 psychology journal entries", "🏗️", "Psychology"),
        new(AchievementType.JournalEntries500, "Consciousness Master", "Write 500 psychology journal entries", "🧬", "Psychology"),
    ];

    // ── Public API ──────────────────────────────────────────────────────

    public async Task<KarmaRecord> AwardKarmaAsync(int userId, KarmaActionType actionType, string description,
        int? referenceId = null, int? overridePoints = null, CancellationToken ct = default)
    {
        int points = overridePoints ?? DefaultPoints.GetValueOrDefault(actionType, 0);

        var record = new KarmaRecord
        {
            Id = 0,
            ActionType = actionType,
            Points = points,
            Description = description,
            ReferenceId = referenceId,
            RecordedAt = DateTimeOffset.UtcNow
        };

        psychologyDb.KarmaRecords.Add(record);
        await psychologyDb.SaveChangesAsync(ct);

        logger.LogInformation(
            "Karma awarded to user {UserId}: {ActionType} ({Points:+#;-#;0}) — {Description}",
            userId, actionType, points, description);

        // Check for new achievements after karma change
        await CheckAndUnlockAchievementsAsync(userId, ct);

        return record;
    }

    public async Task<KarmaSummaryViewModel> GetKarmaSummaryAsync(int userId, CancellationToken ct = default)
    {
        int totalKarma = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId)
            .SumAsync(k => k.Points, ct);

        // Ensure karma doesn't go negative for level calculation
        totalKarma = Math.Max(0, totalKarma);

        var (level, title, pointsToNext, nextThreshold, progress) = CalculateLevel(totalKarma);

        int unlockedAchievements = await psychologyDb.Achievements
            .AsNoTracking()
            .CountAsync(a => a.CreatedBy == userId, ct);

        // Calculate current journaling streak
        int journalingStreak = await CalculateJournalingStreakAsync(userId, ct);

        // Get recent karma events (last 10)
        var recentEvents = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId)
            .OrderByDescending(k => k.RecordedAt)
            .Take(10)
            .Select(k => new KarmaEventViewModel
            {
                ActionType = k.ActionType.ToString(),
                Points = k.Points,
                Description = k.Description,
                RecordedAt = k.RecordedAt
            })
            .ToListAsync(ct);

        return new KarmaSummaryViewModel
        {
            TotalKarma = totalKarma,
            Level = level,
            Title = title,
            PointsToNextLevel = pointsToNext,
            NextLevelThreshold = nextThreshold,
            LevelProgress = progress,
            TotalAchievements = AchievementDefinitions.Length,
            UnlockedAchievements = unlockedAchievements,
            CurrentJournalingStreak = journalingStreak,
            RecentEvents = recentEvents
        };
    }

    public async Task<List<KarmaEventViewModel>> GetKarmaHistoryAsync(int userId, int days = 30, CancellationToken ct = default)
    {
        DateTimeOffset since = DateTimeOffset.UtcNow.AddDays(-days);

        return await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.RecordedAt >= since)
            .OrderByDescending(k => k.RecordedAt)
            .Select(k => new KarmaEventViewModel
            {
                ActionType = k.ActionType.ToString(),
                Points = k.Points,
                Description = k.Description,
                RecordedAt = k.RecordedAt
            })
            .ToListAsync(ct);
    }

    public async Task<List<AchievementViewModel>> GetAchievementsAsync(int userId, CancellationToken ct = default)
    {
        var unlockedSet = await psychologyDb.Achievements
            .AsNoTracking()
            .Where(a => a.CreatedBy == userId)
            .ToDictionaryAsync(a => a.AchievementType, a => a.UnlockedAt, ct);

        return AchievementDefinitions.Select(def => new AchievementViewModel
        {
            Type = def.Type.ToString(),
            Name = def.Name,
            Description = def.Description,
            Emoji = def.Emoji,
            Category = def.Category,
            IsUnlocked = unlockedSet.ContainsKey(def.Type),
            UnlockedAt = unlockedSet.GetValueOrDefault(def.Type)
        }).ToList();
    }

    public async Task<KarmaSummaryViewModel> RecalculateKarmaAsync(int userId, CancellationToken ct = default)
    {
        // Fetch all trade data to recalculate karma from scratch
        var trades = await tradeProvider.GetTradesAsync(userId, ct);
        var closedTrades = trades.Where(t => t.ClosedDate.HasValue).ToList();

        // Clear existing karma records for a clean recalculation
        var existingRecords = await psychologyDb.KarmaRecords
            .Where(k => k.CreatedBy == userId)
            .ToListAsync(ct);

        psychologyDb.KarmaRecords.RemoveRange(existingRecords);
        await psychologyDb.SaveChangesAsync(ct);

        // Award karma for each trade
        foreach (var trade in trades)
        {
            var record = new KarmaRecord
            {
                Id = 0,
                ActionType = KarmaActionType.TradeJournaled,
                Points = DefaultPoints[KarmaActionType.TradeJournaled],
                Description = $"Trade logged: {trade.Asset}",
                ReferenceId = trade.Id,
                RecordedAt = trade.Date
            };
            psychologyDb.KarmaRecords.Add(record);
        }

        // Award karma for rule-broken trades (penalty)
        foreach (var trade in trades.Where(t => t.IsRuleBroken))
        {
            var record = new KarmaRecord
            {
                Id = 0,
                ActionType = KarmaActionType.RuleBrokenPenalty,
                Points = DefaultPoints[KarmaActionType.RuleBrokenPenalty],
                Description = $"Rule broken on trade: {trade.Asset}",
                ReferenceId = trade.Id,
                RecordedAt = trade.Date
            };
            psychologyDb.KarmaRecords.Add(record);
        }

        // Award karma for psychology journal entries
        var journalEntries = await psychologyDb.PsychologyJournals
            .AsNoTracking()
            .Where(j => j.CreatedBy == userId)
            .ToListAsync(ct);

        foreach (var entry in journalEntries)
        {
            var record = new KarmaRecord
            {
                Id = 0,
                ActionType = KarmaActionType.PsychologyJournalEntry,
                Points = DefaultPoints[KarmaActionType.PsychologyJournalEntry],
                Description = "Psychology journal entry",
                ReferenceId = entry.Id,
                RecordedAt = entry.CreatedDate
            };
            psychologyDb.KarmaRecords.Add(record);
        }

        // Calculate win streak bonuses from closed trades
        await AwardWinStreakBonusesAsync(closedTrades);

        await psychologyDb.SaveChangesAsync(ct);

        logger.LogInformation("Karma recalculated for user {UserId}: {TradeCount} trades, {JournalCount} journal entries",
            userId, trades.Count, journalEntries.Count);

        // Re-check all achievements
        await CheckAndUnlockAchievementsAsync(userId, ct);

        return await GetKarmaSummaryAsync(userId, ct);
    }

    // ── Private Helpers ─────────────────────────────────────────────────

    private static (int Level, string Title, int PointsToNext, int NextThreshold, double Progress) CalculateLevel(int totalKarma)
    {
        int level = 1;
        string title = KarmaLevels[0].Title;
        int nextThreshold = KarmaLevels.Length > 1 ? KarmaLevels[1].Threshold : int.MaxValue;
        int currentThreshold = 0;

        for (int i = KarmaLevels.Length - 1; i >= 0; i--)
        {
            if (totalKarma >= KarmaLevels[i].Threshold)
            {
                level = i + 1;
                title = KarmaLevels[i].Title;
                currentThreshold = KarmaLevels[i].Threshold;
                nextThreshold = i + 1 < KarmaLevels.Length ? KarmaLevels[i + 1].Threshold : KarmaLevels[i].Threshold;
                break;
            }
        }

        int pointsToNext = level >= KarmaLevels.Length ? 0 : nextThreshold - totalKarma;
        double progress = level >= KarmaLevels.Length
            ? 100.0
            : nextThreshold == currentThreshold
                ? 100.0
                : (double)(totalKarma - currentThreshold) / (nextThreshold - currentThreshold) * 100.0;

        return (level, title, Math.Max(0, pointsToNext), nextThreshold, Math.Clamp(progress, 0, 100));
    }

    private async Task<int> CalculateJournalingStreakAsync(int userId, CancellationToken ct)
    {
        // Get dates where user has activity (trades or journal entries)
        var tradeDates = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId &&
                   (k.ActionType == KarmaActionType.TradeJournaled ||
                    k.ActionType == KarmaActionType.PsychologyJournalEntry))
            .Select(k => k.RecordedAt.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .Take(365) // Look back at most 1 year
            .ToListAsync(ct);

        if (tradeDates.Count == 0)
            return 0;

        // Count consecutive days from today
        int streak = 0;
        DateTime checkDate = DateTimeOffset.UtcNow.Date;

        // Allow today or yesterday as the start of the streak
        if (!tradeDates.Contains(checkDate))
        {
            checkDate = checkDate.AddDays(-1);
            if (!tradeDates.Contains(checkDate))
                return 0;
        }

        foreach (DateTime date in tradeDates.OrderByDescending(d => d))
        {
            if (date == checkDate)
            {
                streak++;
                checkDate = checkDate.AddDays(-1);
            }
            else if (date < checkDate)
            {
                break;
            }
        }

        return streak;
    }

    private async Task CheckAndUnlockAchievementsAsync(int userId, CancellationToken ct)
    {
        var existingAchievements = await psychologyDb.Achievements
            .Where(a => a.CreatedBy == userId)
            .Select(a => a.AchievementType)
            .ToListAsync(ct);

        int totalKarma = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId)
            .SumAsync(k => k.Points, ct);

        totalKarma = Math.Max(0, totalKarma);
        var (level, _, _, _, _) = CalculateLevel(totalKarma);

        // Get trade count
        int tradeCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.TradeJournaled)
            .CountAsync(ct);

        // Get review count
        int reviewCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.TradeReviewed)
            .CountAsync(ct);

        // Get journaling streak
        int journalingStreak = await CalculateJournalingStreakAsync(userId, ct);

        // Get win streak data
        var latestStreak = await psychologyDb.StreakRecords
            .AsNoTracking()
            .Where(s => s.CreatedBy == userId)
            .OrderByDescending(s => s.RecordedAt)
            .FirstOrDefaultAsync(ct);

        int bestWinStreak = latestStreak?.BestWinStreak ?? 0;

        // Get tilt recovery count
        int tiltRecoveryCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.TiltRecovery)
            .CountAsync(ct);

        // Get disciplined trade count (trades without rule breaks)
        var trades = await tradeProvider.GetTradesAsync(userId, ct);
        int consecutiveDisciplinedTrades = 0;
        foreach (var trade in trades.OrderByDescending(t => t.Date))
        {
            if (!trade.IsRuleBroken)
                consecutiveDisciplinedTrades++;
            else
                break;
        }

        // Get psychology journal entry count
        int journalEntryCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.PsychologyJournalEntry)
            .CountAsync(ct);

        // Check each achievement
        var achievementsToUnlock = new List<(AchievementType type, AchievementDefinition def)>();

        foreach (var def in AchievementDefinitions)
        {
            if (existingAchievements.Contains(def.Type))
                continue;

            bool shouldUnlock = def.Type switch
            {
                // Trade milestones
                AchievementType.FirstTrade => tradeCount >= 1,
                AchievementType.TenTrades => tradeCount >= 10,
                AchievementType.TwentyFiveTrades => tradeCount >= 25,
                AchievementType.FiftyTrades => tradeCount >= 50,
                AchievementType.HundredTrades => tradeCount >= 100,
                AchievementType.TwoFiftyTrades => tradeCount >= 250,
                AchievementType.FiveHundredTrades => tradeCount >= 500,
                AchievementType.ThousandTrades => tradeCount >= 1000,
                AchievementType.TwoThousandFiveHundredTrades => tradeCount >= 2500,
                AchievementType.FiveThousandTrades => tradeCount >= 5000,
                AchievementType.SevenThousandFiveHundredTrades => tradeCount >= 7500,
                AchievementType.TenThousandTrades => tradeCount >= 10000,

                // Review milestones
                AchievementType.FirstReview => reviewCount >= 1,
                AchievementType.FiveReviews => reviewCount >= 5,
                AchievementType.TenReviews => reviewCount >= 10,
                AchievementType.TwentyFiveReviews => reviewCount >= 25,
                AchievementType.FiftyReviews => reviewCount >= 50,
                AchievementType.HundredReviews => reviewCount >= 100,
                AchievementType.TwoHundredReviews => reviewCount >= 200,
                AchievementType.FiveHundredReviews => reviewCount >= 500,

                // Journaling streaks
                AchievementType.ThreeDayStreak => journalingStreak >= 3,
                AchievementType.FiveDayStreak => journalingStreak >= 5,
                AchievementType.WeekStreak => journalingStreak >= 7,
                AchievementType.TwoWeekStreak => journalingStreak >= 14,
                AchievementType.ThreeWeekStreak => journalingStreak >= 21,
                AchievementType.MonthStreak => journalingStreak >= 30,
                AchievementType.FortyFiveDayStreak => journalingStreak >= 45,
                AchievementType.SixtyDayStreak => journalingStreak >= 60,
                AchievementType.QuarterStreak => journalingStreak >= 90,
                AchievementType.FourMonthStreak => journalingStreak >= 120,
                AchievementType.HalfYearStreak => journalingStreak >= 180,
                AchievementType.EightMonthStreak => journalingStreak >= 240,
                AchievementType.TenMonthStreak => journalingStreak >= 300,
                AchievementType.YearStreak => journalingStreak >= 365,

                // Win streaks
                AchievementType.WinStreak3 => bestWinStreak >= 3,
                AchievementType.WinStreak5 => bestWinStreak >= 5,
                AchievementType.WinStreak7 => bestWinStreak >= 7,
                AchievementType.WinStreak10 => bestWinStreak >= 10,
                AchievementType.WinStreak15 => bestWinStreak >= 15,
                AchievementType.WinStreak20 => bestWinStreak >= 20,
                AchievementType.WinStreak25 => bestWinStreak >= 25,
                AchievementType.WinStreak30 => bestWinStreak >= 30,

                // Karma levels
                AchievementType.KarmaLevel2 => level >= 2,
                AchievementType.KarmaLevel3 => level >= 3,
                AchievementType.KarmaLevel5 => level >= 5,
                AchievementType.KarmaLevel7 => level >= 7,
                AchievementType.KarmaLevel10 => level >= 10,
                AchievementType.KarmaLevel12 => level >= 12,
                AchievementType.KarmaLevel15 => level >= 15,
                AchievementType.KarmaLevel18 => level >= 18,
                AchievementType.KarmaLevel20 => level >= 20,
                AchievementType.KarmaLevel25 => level >= 25,

                // Psychology — tilt
                AchievementType.TiltRecovery1 => tiltRecoveryCount >= 1,
                AchievementType.TiltRecovery3 => tiltRecoveryCount >= 3,
                AchievementType.TiltMaster => tiltRecoveryCount >= 5,
                AchievementType.TiltGuru => tiltRecoveryCount >= 10,
                AchievementType.TiltRecovery15 => tiltRecoveryCount >= 15,
                AchievementType.TiltEnlightened => tiltRecoveryCount >= 25,
                AchievementType.TiltRecovery50 => tiltRecoveryCount >= 50,

                // Psychology — discipline
                AchievementType.Disciplined => consecutiveDisciplinedTrades >= 20,
                AchievementType.Disciplined30 => consecutiveDisciplinedTrades >= 30,
                AchievementType.Disciplined50 => consecutiveDisciplinedTrades >= 50,
                AchievementType.Disciplined100 => consecutiveDisciplinedTrades >= 100,
                AchievementType.Disciplined200 => consecutiveDisciplinedTrades >= 200,
                AchievementType.Disciplined300 => consecutiveDisciplinedTrades >= 300,
                AchievementType.Disciplined500 => consecutiveDisciplinedTrades >= 500,

                // Psychology — journal entries
                AchievementType.JournalEntries5 => journalEntryCount >= 5,
                AchievementType.JournalEntries10 => journalEntryCount >= 10,
                AchievementType.JournalEntries25 => journalEntryCount >= 25,
                AchievementType.JournalEntries50 => journalEntryCount >= 50,
                AchievementType.JournalEntries100 => journalEntryCount >= 100,
                AchievementType.JournalEntries250 => journalEntryCount >= 250,
                AchievementType.JournalEntries500 => journalEntryCount >= 500,

                _ => false
            };

            if (shouldUnlock)
            {
                achievementsToUnlock.Add((def.Type, def));
            }
        }

        // Persist unlocked achievements and fire events
        foreach (var (type, def) in achievementsToUnlock)
        {
            var achievement = new Achievement
            {
                Id = 0,
                AchievementType = type,
                UnlockedAt = DateTimeOffset.UtcNow
            };

            psychologyDb.Achievements.Add(achievement);
            await psychologyDb.SaveChangesAsync(ct);

            logger.LogInformation("🏆 Achievement unlocked for user {UserId}: {AchievementName} {Emoji}",
                userId, def.Name, def.Emoji);

            await eventBus.PublishAsync(new KarmaAchievementEvent(
                EventId: Guid.NewGuid(),
                UserId: userId,
                AchievementName: def.Name,
                AchievementDescription: def.Description,
                Emoji: def.Emoji,
                TotalKarma: totalKarma,
                KarmaLevel: level,
                KarmaTitle: CalculateLevel(totalKarma).Title), ct);
        }
    }

    private async Task AwardWinStreakBonusesAsync(List<TradeCacheDto> closedTrades)
    {
        if (closedTrades.Count == 0) return;

        // Walk chronologically and find streak transitions
        var orderedTrades = closedTrades.OrderBy(t => t.ClosedDate ?? t.Date).ToList();
        int currentWinStreak = 0;

        foreach (var trade in orderedTrades)
        {
            if (!trade.Pnl.HasValue) continue;

            if (trade.Pnl.Value > 0)
            {
                currentWinStreak++;

                // Award streak bonus at milestones (every 5 consecutive wins)
                if (currentWinStreak > 0 && currentWinStreak % 5 == 0)
                {
                    int bonusPoints = DefaultPoints[KarmaActionType.WinStreakBonus] * currentWinStreak;
                    var record = new KarmaRecord
                    {
                        Id = 0,
                        ActionType = KarmaActionType.WinStreakBonus,
                        Points = bonusPoints,
                        Description = $"Win streak bonus: {currentWinStreak} consecutive wins",
                        ReferenceId = trade.Id,
                        RecordedAt = trade.ClosedDate ?? trade.Date
                    };
                    psychologyDb.KarmaRecords.Add(record);
                }
            }
            else
            {
                currentWinStreak = 0;
            }
        }
    }

    // ── Inner Types ─────────────────────────────────────────────────────

    private sealed record AchievementDefinition(
        AchievementType Type,
        string Name,
        string Description,
        string Emoji,
        string Category);
}
