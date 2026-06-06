using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Psychology.Events;
using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Services;

internal sealed partial class KarmaService
{
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

}
