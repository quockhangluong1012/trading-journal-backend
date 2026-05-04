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

    // ── Karma Levels ────────────────────────────────────────────────────

    private static readonly (int Threshold, string Title)[] KarmaLevels =
    [
        (0, "Novice Trader"),
        (50, "Apprentice"),
        (150, "Journeyman"),
        (300, "Skilled Trader"),
        (500, "Expert"),
        (750, "Master Trader"),
        (1100, "Grandmaster"),
        (1500, "Elite"),
        (2000, "Legend"),
        (3000, "Trading Sage"),
    ];

    // ── Achievement Definitions ─────────────────────────────────────────

    private static readonly AchievementDefinition[] AchievementDefinitions =
    [
        // Trade milestones
        new(AchievementType.FirstTrade, "First Blood", "Log your first trade", "🎯", "Trades"),
        new(AchievementType.TenTrades, "Getting Started", "Log 10 trades", "📊", "Trades"),
        new(AchievementType.HundredTrades, "Century Trader", "Log 100 trades", "💯", "Trades"),
        new(AchievementType.ThousandTrades, "Trading Machine", "Log 1,000 trades", "🏆", "Trades"),

        // Review milestones
        new(AchievementType.FirstReview, "Self-Aware", "Complete your first trade review", "📝", "Reviews"),

        // Journaling streaks
        new(AchievementType.WeekStreak, "Week Warrior", "7-day journaling streak", "🔥", "Streaks"),
        new(AchievementType.MonthStreak, "Monthly Master", "30-day journaling streak", "⚡", "Streaks"),
        new(AchievementType.QuarterStreak, "Quarter Legend", "90-day journaling streak", "💎", "Streaks"),

        // Win streaks
        new(AchievementType.WinStreak5, "Hot Hand", "5 consecutive winning trades", "🎰", "Performance"),
        new(AchievementType.WinStreak10, "Unstoppable", "10 consecutive winning trades", "🌟", "Performance"),

        // Karma levels
        new(AchievementType.KarmaLevel5, "Rising Star", "Reach karma level 5", "⭐", "Karma"),
        new(AchievementType.KarmaLevel10, "Moonwalker", "Reach karma level 10", "🌙", "Karma"),
        new(AchievementType.KarmaLevel25, "Trading Royalty", "Reach karma level 25", "👑", "Karma"),

        // Psychology
        new(AchievementType.TiltMaster, "Zen Master", "Recover from tilt 5 times", "🧘", "Psychology"),
        new(AchievementType.Disciplined, "Iron Discipline", "20 trades with no rule breaks", "🎖️", "Psychology"),
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
            RecordedAt = DateTime.UtcNow
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
        DateTime since = DateTime.UtcNow.AddDays(-days);

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
        DateTime checkDate = DateTime.UtcNow.Date;

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

        // Check each achievement
        var achievementsToUnlock = new List<(AchievementType type, AchievementDefinition def)>();

        foreach (var def in AchievementDefinitions)
        {
            if (existingAchievements.Contains(def.Type))
                continue;

            bool shouldUnlock = def.Type switch
            {
                AchievementType.FirstTrade => tradeCount >= 1,
                AchievementType.TenTrades => tradeCount >= 10,
                AchievementType.HundredTrades => tradeCount >= 100,
                AchievementType.ThousandTrades => tradeCount >= 1000,
                AchievementType.FirstReview => reviewCount >= 1,
                AchievementType.WeekStreak => journalingStreak >= 7,
                AchievementType.MonthStreak => journalingStreak >= 30,
                AchievementType.QuarterStreak => journalingStreak >= 90,
                AchievementType.WinStreak5 => bestWinStreak >= 5,
                AchievementType.WinStreak10 => bestWinStreak >= 10,
                AchievementType.KarmaLevel5 => level >= 5,
                AchievementType.KarmaLevel10 => level >= 10,
                AchievementType.KarmaLevel25 => level >= 25,
                AchievementType.TiltMaster => tiltRecoveryCount >= 5,
                AchievementType.Disciplined => consecutiveDisciplinedTrades >= 20,
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
                UnlockedAt = DateTime.UtcNow
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
