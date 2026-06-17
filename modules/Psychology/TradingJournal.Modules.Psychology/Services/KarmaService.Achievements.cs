using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Psychology.Events;
using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Services;

internal sealed partial class KarmaService
{
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
            .Take(730) // Look back far enough for the two-year streak achievement
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

        // Get psychology journal entry count
        int journalEntryCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .Where(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.PsychologyJournalEntry)
            .CountAsync(ct);

        int completedTaskCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .CountAsync(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.GoalTaskCompleted, ct);
        int completedMilestoneCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .CountAsync(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.GoalMilestoneCompleted, ct);
        int completedGoalCount = await psychologyDb.KarmaRecords
            .AsNoTracking()
            .CountAsync(k => k.CreatedBy == userId && k.ActionType == KarmaActionType.GoalCompleted, ct);

        // ── New: Daily note streak ──
        int dailyNoteStreak = await CalculateDailyNoteStreakAsync(userId, ct);

        // ── New: Skill metrics from trade data ──
        var closedTrades = trades.Where(t => t.ClosedDate.HasValue && t.Pnl.HasValue).ToList();
        var (rr2xCount, rr3xCount) = CalculateRiskRewardCounts(closedTrades);
        int recoveryCount = CalculateLossRecoveryCount(closedTrades);
        var (winRate, closedCount) = CalculateWinRate(closedTrades);
        int uniqueAssets = trades.Select(t => t.Asset).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int uniqueSetups = trades.Where(t => t.TradingSetupId.HasValue).Select(t => t.TradingSetupId!.Value).Distinct().Count();

        // ── New: ICT methodology metrics ──
        var ictMetrics = CalculateIctMetrics(trades, closedTrades);

        // ── New: Advanced R:R metrics ──
        var (rr5xCount, rr10xCount) = CalculateAdvancedRiskRewardCounts(closedTrades);
        var profitMetrics = CalculateProfitMetrics(closedTrades);
        var propMetrics = CalculatePropFirmMetrics(closedTrades, winRate, closedCount, consecutiveDisciplinedTrades);
        var hardMetrics = CalculateHardMetrics(closedTrades, bestWinStreak, consecutiveDisciplinedTrades, winRate, closedCount, tradeCount, level, journalingStreak, ictMetrics, existingAchievements);

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
                AchievementType.FifteenHundredTrades => tradeCount >= 1500,
                AchievementType.TwoThousandFiveHundredTrades => tradeCount >= 2500,
                AchievementType.ThreeThousandTrades => tradeCount >= 3000,
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
                AchievementType.ThreeHundredReviews => reviewCount >= 300,
                AchievementType.FiveHundredReviews => reviewCount >= 500,
                AchievementType.ThousandReviews => reviewCount >= 1000,

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
                AchievementType.TwoYearStreak => journalingStreak >= 730,

                // Win streaks
                AchievementType.WinStreak3 => bestWinStreak >= 3,
                AchievementType.WinStreak5 => bestWinStreak >= 5,
                AchievementType.WinStreak7 => bestWinStreak >= 7,
                AchievementType.WinStreak10 => bestWinStreak >= 10,
                AchievementType.WinStreak15 => bestWinStreak >= 15,
                AchievementType.WinStreak20 => bestWinStreak >= 20,
                AchievementType.WinStreak25 => bestWinStreak >= 25,
                AchievementType.WinStreak30 => bestWinStreak >= 30,
                AchievementType.WinStreak40 => bestWinStreak >= 40,
                AchievementType.WinStreak50 => bestWinStreak >= 50,

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
                AchievementType.KarmaLevel22 => level >= 22,
                AchievementType.KarmaLevel24 => level >= 24,
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
                AchievementType.Disciplined1000 => consecutiveDisciplinedTrades >= 1000,

                // Psychology — journal entries
                AchievementType.JournalEntries5 => journalEntryCount >= 5,
                AchievementType.JournalEntries10 => journalEntryCount >= 10,
                AchievementType.JournalEntries25 => journalEntryCount >= 25,
                AchievementType.JournalEntries50 => journalEntryCount >= 50,
                AchievementType.JournalEntries100 => journalEntryCount >= 100,
                AchievementType.JournalEntries250 => journalEntryCount >= 250,
                AchievementType.JournalEntries500 => journalEntryCount >= 500,
                AchievementType.JournalEntries1000 => journalEntryCount >= 1000,

                // Daily note preparation
                AchievementType.DailyNotes3 => dailyNoteStreak >= 3,
                AchievementType.DailyNotes7 => dailyNoteStreak >= 7,
                AchievementType.DailyNotes14 => dailyNoteStreak >= 14,
                AchievementType.DailyNotes30 => dailyNoteStreak >= 30,
                AchievementType.DailyNotes60 => dailyNoteStreak >= 60,
                AchievementType.DailyNotes90 => dailyNoteStreak >= 90,
                AchievementType.DailyNotes180 => dailyNoteStreak >= 180,
                AchievementType.DailyNotes365 => dailyNoteStreak >= 365,

                // Risk management (R:R ratio)
                AchievementType.RiskReward2x10 => rr2xCount >= 10,
                AchievementType.RiskReward2x25 => rr2xCount >= 25,
                AchievementType.RiskReward2x50 => rr2xCount >= 50,
                AchievementType.RiskReward3x10 => rr3xCount >= 10,
                AchievementType.RiskReward3x25 => rr3xCount >= 25,
                AchievementType.RiskReward3x50 => rr3xCount >= 50,
                AchievementType.RiskReward2x100 => rr2xCount >= 100,
                AchievementType.RiskReward3x100 => rr3xCount >= 100,

                // Loss recovery
                AchievementType.Recovery3 => recoveryCount >= 1,
                AchievementType.Recovery5 => recoveryCount >= 5,
                AchievementType.Recovery10 => recoveryCount >= 10,
                AchievementType.Recovery25 => recoveryCount >= 25,
                AchievementType.Recovery50 => recoveryCount >= 50,

                // Win rate milestones (require minimum sample size)
                AchievementType.WinRate50 => closedCount >= 30 && winRate >= 50.0,
                AchievementType.WinRate55 => closedCount >= 50 && winRate >= 55.0,
                AchievementType.WinRate60 => closedCount >= 75 && winRate >= 60.0,
                AchievementType.WinRate65 => closedCount >= 100 && winRate >= 65.0,
                AchievementType.WinRate70 => closedCount >= 150 && winRate >= 70.0,
                AchievementType.WinRate75 => closedCount >= 200 && winRate >= 75.0,
                AchievementType.WinRate80 => closedCount >= 250 && winRate >= 80.0,

                // Diversification
                AchievementType.Assets5 => uniqueAssets >= 5,
                AchievementType.Assets10 => uniqueAssets >= 10,
                AchievementType.Assets20 => uniqueAssets >= 20,
                AchievementType.Setups5 => uniqueSetups >= 5,
                AchievementType.Assets30 => uniqueAssets >= 30,
                AchievementType.Setups10 => uniqueSetups >= 10,

                // ICT Methodology
                AchievementType.IctPo3First => ictMetrics.Po3Count >= 1,
                AchievementType.IctPo3_10 => ictMetrics.Po3Count >= 10,
                AchievementType.IctPo3_25 => ictMetrics.Po3Count >= 25,
                AchievementType.IctPo3_50 => ictMetrics.Po3Count >= 50,

                AchievementType.IctDiscountEntry5 => ictMetrics.DiscountEntryCount >= 5,
                AchievementType.IctDiscountEntry25 => ictMetrics.DiscountEntryCount >= 25,
                AchievementType.IctDiscountEntry50 => ictMetrics.DiscountEntryCount >= 50,

                AchievementType.IctMarketStructure5 => ictMetrics.MarketStructureCount >= 5,
                AchievementType.IctMarketStructure25 => ictMetrics.MarketStructureCount >= 25,
                AchievementType.IctMarketStructure50 => ictMetrics.MarketStructureCount >= 50,

                AchievementType.IctBiasAligned5 => ictMetrics.BiasAlignedCount >= 5,
                AchievementType.IctBiasAligned25 => ictMetrics.BiasAlignedCount >= 25,
                AchievementType.IctBiasAligned50 => ictMetrics.BiasAlignedCount >= 50,
                AchievementType.IctBiasAlignedWin10 => ictMetrics.BiasAlignedWinCount >= 10,

                AchievementType.IctKillzone10 => ictMetrics.KillzoneCount >= 10,
                AchievementType.IctKillzone50 => ictMetrics.KillzoneCount >= 50,
                AchievementType.IctKillzone100 => ictMetrics.KillzoneCount >= 100,

                AchievementType.IctComplete5 => ictMetrics.CompleteIctCount >= 5,
                AchievementType.IctComplete25 => ictMetrics.CompleteIctCount >= 25,
                AchievementType.IctComplete50 => ictMetrics.CompleteIctCount >= 50,

                // ICT Extended Mastery
                AchievementType.IctPremiumEntry5 => ictMetrics.PremiumEntryCount >= 5,
                AchievementType.IctPremiumEntry25 => ictMetrics.PremiumEntryCount >= 25,
                AchievementType.IctPremiumEntry50 => ictMetrics.PremiumEntryCount >= 50,
                AchievementType.IctBosFirst => ictMetrics.BosCount >= 1,
                AchievementType.IctBos25 => ictMetrics.BosCount >= 25,
                AchievementType.IctChoch10 => ictMetrics.ChochCount >= 10,
                AchievementType.IctChoch25 => ictMetrics.ChochCount >= 25,
                AchievementType.IctDistribution10 => ictMetrics.DistributionCount >= 10,
                AchievementType.IctDistribution25 => ictMetrics.DistributionCount >= 25,
                AchievementType.IctManipulation10 => ictMetrics.ManipulationCount >= 10,
                AchievementType.IctAccumulation10 => ictMetrics.AccumulationCount >= 10,
                AchievementType.IctConfluentWin5 => ictMetrics.ConfluentWinCount >= 5,
                AchievementType.IctConfluentWin25 => ictMetrics.ConfluentWinCount >= 25,
                AchievementType.IctKillzoneWin10 => ictMetrics.KillzoneWinCount >= 10,
                AchievementType.IctKillzoneWin25 => ictMetrics.KillzoneWinCount >= 25,

                // Profit & Advanced R:R
                AchievementType.RiskReward5x5 => rr5xCount >= 5,
                AchievementType.RiskReward5x25 => rr5xCount >= 25,
                AchievementType.RiskReward10x1 => rr10xCount >= 1,
                AchievementType.RiskReward10x5 => rr10xCount >= 5,
                AchievementType.ProfitableDay10 => profitMetrics.ProfitableDays >= 10,
                AchievementType.ProfitableDay25 => profitMetrics.ProfitableDays >= 25,
                AchievementType.ProfitableDay50 => profitMetrics.ProfitableDays >= 50,
                AchievementType.ProfitableDay100 => profitMetrics.ProfitableDays >= 100,
                AchievementType.ProfitableDay250 => profitMetrics.ProfitableDays >= 250,
                AchievementType.ProfitableWeek5 => profitMetrics.ProfitableWeeks >= 5,
                AchievementType.ProfitableWeek10 => profitMetrics.ProfitableWeeks >= 10,
                AchievementType.ProfitableWeek25 => profitMetrics.ProfitableWeeks >= 25,
                AchievementType.ProfitableWeek50 => profitMetrics.ProfitableWeeks >= 50,
                AchievementType.BestTradeRR5 => profitMetrics.BestTradeRR >= 5.0,
                AchievementType.BestTradeRR10 => profitMetrics.BestTradeRR >= 10.0,
                AchievementType.BestTradeRR20 => profitMetrics.BestTradeRR >= 20.0,

                // Prop Firm Challenge
                AchievementType.PropMinDays5 => propMetrics.BestMonthTradingDays >= 5,
                AchievementType.PropMinDays10 => propMetrics.BestMonthTradingDays >= 10,
                AchievementType.PropMinDays20 => propMetrics.BestMonthTradingDays >= 20,
                AchievementType.PropNoDailyLoss5 => propMetrics.MaxNoDailyLossStreak >= 5,
                AchievementType.PropNoDailyLoss10 => propMetrics.MaxNoDailyLossStreak >= 10,
                AchievementType.PropNoDailyLoss20 => propMetrics.MaxNoDailyLossStreak >= 20,
                AchievementType.PropConsistency10 => propMetrics.ConsistencyDays >= 10,
                AchievementType.PropConsistency30 => propMetrics.ConsistencyDays >= 30,
                AchievementType.PropPhase1 => propMetrics.Phase1Passed,
                AchievementType.PropPhase2 => propMetrics.Phase2Passed,
                AchievementType.PropFundedReady => propMetrics.FundedReady,

                // Hard / Elite
                AchievementType.PerfectWeek => hardMetrics.HasPerfectWeek,
                AchievementType.Sniper3Consecutive => hardMetrics.MaxConsecutiveSniperRR3 >= 3,
                AchievementType.Sniper5Consecutive => hardMetrics.MaxConsecutiveSniperRR3 >= 5,
                AchievementType.IronmanTrader => hardMetrics.IsIronman,
                AchievementType.IctSamurai => hardMetrics.IsIctSamurai,
                AchievementType.ZenPerfection => hardMetrics.IsZenPerfection,
                AchievementType.MarathonTrader => hardMetrics.IsMarathonTrader,
                AchievementType.EliteStatus => hardMetrics.IsEliteStatus,
                AchievementType.LegendaryTrader => hardMetrics.IsLegendaryTrader,
                AchievementType.PropFirmGod => hardMetrics.IsPropFirmGod,

                // Goal progress
                AchievementType.FirstGoalTaskCompleted => completedTaskCount >= 1,
                AchievementType.TenGoalTasksCompleted => completedTaskCount >= 10,
                AchievementType.TwentyFiveGoalTasksCompleted => completedTaskCount >= 25,
                AchievementType.FirstGoalMilestoneCompleted => completedMilestoneCount >= 1,
                AchievementType.FiveGoalMilestonesCompleted => completedMilestoneCount >= 5,
                AchievementType.TenGoalMilestonesCompleted => completedMilestoneCount >= 10,
                AchievementType.FirstGoalCompleted => completedGoalCount >= 1,
                AchievementType.FiveGoalsCompleted => completedGoalCount >= 5,
                AchievementType.TenGoalsCompleted => completedGoalCount >= 10,
                AchievementType.GoalMaster => completedGoalCount >= 25,

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
                UnlockedAt = DateTime.UtcNow,
                CreatedBy = userId,
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

}
