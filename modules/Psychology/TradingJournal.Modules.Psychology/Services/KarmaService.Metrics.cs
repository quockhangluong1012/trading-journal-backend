using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Psychology.Events;
using TradingJournal.Modules.Psychology.ViewModel;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Services;

internal sealed partial class KarmaService
{
    /// <summary>
    /// Calculates consecutive days with daily notes, counting backward from today.
    /// </summary>
    private async Task<int> CalculateDailyNoteStreakAsync(int userId, CancellationToken ct)
    {
        var noteDates = await psychologyDb.DailyNotes
            .AsNoTracking()
            .Where(n => n.CreatedBy == userId)
            .Select(n => n.NoteDate)
            .Distinct()
            .OrderByDescending(d => d)
            .Take(365)
            .ToListAsync(ct);

        if (noteDates.Count == 0)
            return 0;

        int streak = 0;
        var checkDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        // Allow today or yesterday as the start
        if (!noteDates.Contains(checkDate))
        {
            checkDate = checkDate.AddDays(-1);
            if (!noteDates.Contains(checkDate))
                return 0;
        }

        foreach (var date in noteDates.OrderByDescending(d => d))
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

    /// <summary>
    /// Counts trades with reward-to-risk ratio >= 2:1 and >= 3:1.
    /// R:R is calculated as (TargetTier1 - Entry) / (Entry - StopLoss) for longs, inverted for shorts.
    /// </summary>
    private static (int Rr2xCount, int Rr3xCount) CalculateRiskRewardCounts(List<TradeCacheDto> closedTrades)
    {
        int rr2x = 0, rr3x = 0;

        foreach (var trade in closedTrades)
        {
            decimal risk = Math.Abs(trade.EntryPrice - trade.StopLoss);
            if (risk == 0) continue;

            decimal reward = Math.Abs(trade.TargetTier1 - trade.EntryPrice);
            decimal ratio = reward / risk;

            if (ratio >= 2m) rr2x++;
            if (ratio >= 3m) rr3x++;
        }

        return (rr2x, rr3x);
    }

    /// <summary>
    /// Counts the number of times the trader recovered with a win after 3+ consecutive losses.
    /// </summary>
    private static int CalculateLossRecoveryCount(List<TradeCacheDto> closedTrades)
    {
        var ordered = closedTrades.OrderBy(t => t.ClosedDate ?? t.Date).ToList();
        int recoveries = 0;
        int lossStreak = 0;

        foreach (var trade in ordered)
        {
            if (!trade.Pnl.HasValue) continue;

            if (trade.Pnl.Value <= 0)
            {
                lossStreak++;
            }
            else
            {
                if (lossStreak >= 3)
                    recoveries++;
                lossStreak = 0;
            }
        }

        return recoveries;
    }

    /// <summary>
    /// Calculates the overall win rate from closed trades.
    /// </summary>
    private static (double WinRate, int ClosedCount) CalculateWinRate(List<TradeCacheDto> closedTrades)
    {
        if (closedTrades.Count == 0)
            return (0, 0);

        int wins = closedTrades.Count(t => t.Pnl.HasValue && t.Pnl.Value > 0);
        double winRate = (double)wins / closedTrades.Count * 100.0;
        return (winRate, closedTrades.Count);
    }

    /// <summary>
    /// Calculates all ICT methodology metrics from trade data.
    /// </summary>
    private static IctMetricsResult CalculateIctMetrics(List<TradeCacheDto> allTrades, List<TradeCacheDto> closedTrades)
    {
        // PO3 (AMD): trades with PowerOf3Phase set
        int po3Count = allTrades.Count(t => t.PowerOf3Phase.HasValue);

        // Discount entries: PremiumDiscount == 1 (Discount)
        int discountCount = allTrades.Count(t => t.PremiumDiscount == 1);

        // Premium entries: PremiumDiscount == 0 (Premium)
        int premiumCount = allTrades.Count(t => t.PremiumDiscount == 0);

        // Market structure: trades with MarketStructure set
        int msCount = allTrades.Count(t => t.MarketStructure.HasValue);

        // Specific market structure types
        int bosCount = allTrades.Count(t => t.MarketStructure == 0); // BOS
        int chochCount = allTrades.Count(t => t.MarketStructure == 1); // CHoCH

        // Specific PO3 phases
        int accumulationCount = allTrades.Count(t => t.PowerOf3Phase == 0);
        int manipulationCount = allTrades.Count(t => t.PowerOf3Phase == 1);
        int distributionCount = allTrades.Count(t => t.PowerOf3Phase == 2);

        // Bias aligned: trade direction matches DailyBias
        int biasAligned = allTrades.Count(t =>
            t.DailyBias.HasValue &&
            ((t.DailyBias == 0 && (int)t.Position == 0) ||
             (t.DailyBias == 1 && (int)t.Position == 1)));

        int biasAlignedWins = closedTrades.Count(t =>
            t.DailyBias.HasValue &&
            t.Pnl.HasValue && t.Pnl.Value > 0 &&
            ((t.DailyBias == 0 && (int)t.Position == 0) ||
             (t.DailyBias == 1 && (int)t.Position == 1)));

        // Killzone: trades with a TradingZoneId assigned
        int killzoneCount = allTrades.Count(t => t.TradingZoneId.HasValue);

        // Killzone wins: winning trades in killzone
        int killzoneWinCount = closedTrades.Count(t =>
            t.TradingZoneId.HasValue && t.Pnl.HasValue && t.Pnl.Value > 0);

        // Complete ICT: trades with ALL 4 ICT fields filled
        int completeCount = allTrades.Count(t =>
            t.PowerOf3Phase.HasValue &&
            t.DailyBias.HasValue &&
            t.MarketStructure.HasValue &&
            t.PremiumDiscount.HasValue);

        // Confluent wins: complete ICT + winning + bias aligned
        int confluentWinCount = closedTrades.Count(t =>
            t.PowerOf3Phase.HasValue &&
            t.DailyBias.HasValue &&
            t.MarketStructure.HasValue &&
            t.PremiumDiscount.HasValue &&
            t.Pnl.HasValue && t.Pnl.Value > 0 &&
            ((t.DailyBias == 0 && (int)t.Position == 0) ||
             (t.DailyBias == 1 && (int)t.Position == 1)));

        return new IctMetricsResult(
            po3Count, discountCount, premiumCount, msCount,
            bosCount, chochCount,
            accumulationCount, manipulationCount, distributionCount,
            biasAligned, biasAlignedWins,
            killzoneCount, killzoneWinCount,
            completeCount, confluentWinCount);
    }

    private sealed record IctMetricsResult(
        int Po3Count,
        int DiscountEntryCount,
        int PremiumEntryCount,
        int MarketStructureCount,
        int BosCount,
        int ChochCount,
        int AccumulationCount,
        int ManipulationCount,
        int DistributionCount,
        int BiasAlignedCount,
        int BiasAlignedWinCount,
        int KillzoneCount,
        int KillzoneWinCount,
        int CompleteIctCount,
        int ConfluentWinCount);

    /// <summary>
    /// Counts trades with R:R >= 5:1 and >= 10:1.
    /// </summary>
    private static (int Rr5xCount, int Rr10xCount) CalculateAdvancedRiskRewardCounts(List<TradeCacheDto> closedTrades)
    {
        int rr5x = 0, rr10x = 0;
        foreach (var trade in closedTrades)
        {
            decimal risk = Math.Abs(trade.EntryPrice - trade.StopLoss);
            if (risk == 0) continue;
            decimal reward = Math.Abs(trade.TargetTier1 - trade.EntryPrice);
            decimal ratio = reward / risk;
            if (ratio >= 5m) rr5x++;
            if (ratio >= 10m) rr10x++;
        }
        return (rr5x, rr10x);
    }

    /// <summary>
    /// Calculates profit-related metrics: profitable days, weeks, best single trade R.
    /// </summary>
    private static ProfitMetricsResult CalculateProfitMetrics(List<TradeCacheDto> closedTrades)
    {
        if (closedTrades.Count == 0)
            return new ProfitMetricsResult(0, 0, 0.0);

        // Profitable days: group by close date, sum PnL per day
        int profitableDays = closedTrades
            .GroupBy(t => (t.ClosedDate ?? t.Date).Date)
            .Count(g => g.Sum(t => t.Pnl ?? 0) > 0);

        // Profitable weeks: group by ISO week
        int profitableWeeks = closedTrades
            .GroupBy(t =>
            {
                var d = (t.ClosedDate ?? t.Date).Date;
                int diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
                return d.AddDays(-diff); // Monday of that week
            })
            .Count(g => g.Sum(t => t.Pnl ?? 0) > 0);

        // Best single trade achieved R:R
        double bestRR = 0;
        foreach (var trade in closedTrades.Where(t => t.Pnl > 0 && t.ExitPrice.HasValue))
        {
            decimal risk = Math.Abs(trade.EntryPrice - trade.StopLoss);
            if (risk == 0) continue;
            decimal actualMove = Math.Abs(trade.ExitPrice!.Value - trade.EntryPrice);
            double achievedR = (double)(actualMove / risk);
            if (achievedR > bestRR) bestRR = achievedR;
        }

        return new ProfitMetricsResult(profitableDays, profitableWeeks, bestRR);
    }

    private sealed record ProfitMetricsResult(int ProfitableDays, int ProfitableWeeks, double BestTradeRR);

    /// <summary>
    /// Calculates prop firm challenge metrics.
    /// </summary>
    private static PropFirmMetricsResult CalculatePropFirmMetrics(
        List<TradeCacheDto> closedTrades, double winRate, int closedCount, int disciplinedStreak)
    {
        if (closedTrades.Count == 0)
            return new PropFirmMetricsResult(0, 0, 0, false, false, false);

        // Best month trading days: most unique trading days in any calendar month
        int bestMonthDays = closedTrades
            .GroupBy(t => new { (t.ClosedDate ?? t.Date).Year, (t.ClosedDate ?? t.Date).Month })
            .Max(g => g.Select(t => (t.ClosedDate ?? t.Date).Date).Distinct().Count());

        // Daily PnL for consecutive no-loss-day calculation
        var dailyPnl = closedTrades
            .GroupBy(t => (t.ClosedDate ?? t.Date).Date)
            .Select(g => new { Date = g.Key, Pnl = g.Sum(t => t.Pnl ?? 0) })
            .OrderBy(d => d.Date)
            .ToList();

        // Max consecutive trading days without a losing day
        int maxNoDailyLoss = 0, currentNoDailyLoss = 0;
        foreach (var day in dailyPnl)
        {
            if (day.Pnl >= 0) { currentNoDailyLoss++; maxNoDailyLoss = Math.Max(maxNoDailyLoss, currentNoDailyLoss); }
            else { currentNoDailyLoss = 0; }
        }

        // Consistency rule: days where no single day > 40% of total period profit
        // Count days in rolling 30-day windows that pass consistency
        int consistencyDays = 0;
        if (dailyPnl.Count > 0)
        {
            decimal totalPnl = dailyPnl.Sum(d => d.Pnl);
            if (totalPnl > 0)
            {
                consistencyDays = dailyPnl.Count(d => d.Pnl <= totalPnl * 0.4m);
            }
        }

        // Phase 1: cumulative profit >= 8% equivalent (use total positive PnL ratio)
        // Simplified: at least 8 profitable days with avg R:R >= 1.5, no day losing > 5% of gains
        decimal totalProfit = dailyPnl.Where(d => d.Pnl > 0).Sum(d => d.Pnl);
        decimal totalLoss = dailyPnl.Where(d => d.Pnl < 0).Sum(d => Math.Abs(d.Pnl));
        decimal worstDay = dailyPnl.Count > 0 ? dailyPnl.Min(d => d.Pnl) : 0;

        bool phase1 = closedCount >= 20 &&
                      totalProfit > 0 &&
                      totalProfit > totalLoss &&
                      (totalLoss == 0 || Math.Abs(worstDay) < totalProfit * 0.5m) &&
                      winRate >= 45.0;

        bool phase2 = phase1 &&
                      closedCount >= 40 &&
                      winRate >= 50.0 &&
                      maxNoDailyLoss >= 5;

        // Funded ready: WR 55%+, disciplined 30+, 50+ trades
        bool fundedReady = closedCount >= 50 &&
                           winRate >= 55.0 &&
                           disciplinedStreak >= 30;

        return new PropFirmMetricsResult(bestMonthDays, maxNoDailyLoss, consistencyDays, phase1, phase2, fundedReady);
    }

    private sealed record PropFirmMetricsResult(
        int BestMonthTradingDays, int MaxNoDailyLossStreak, int ConsistencyDays,
        bool Phase1Passed, bool Phase2Passed, bool FundedReady);

    /// <summary>
    /// Calculates hard/elite achievement conditions.
    /// </summary>
    private static HardMetricsResult CalculateHardMetrics(
        List<TradeCacheDto> closedTrades, int bestWinStreak, int disciplinedStreak,
        double winRate, int closedCount, int tradeCount, int karmaLevel, int journalingStreak,
        IctMetricsResult ictMetrics, List<AchievementType> existingAchievements)
    {
        // Perfect week: any calendar week where all trades are winners (min 3)
        bool hasPerfectWeek = closedTrades
            .GroupBy(t =>
            {
                var d = (t.ClosedDate ?? t.Date).Date;
                int diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
                return d.AddDays(-diff);
            })
            .Any(g =>
            {
                var weekTrades = g.Where(t => t.Pnl.HasValue).ToList();
                return weekTrades.Count >= 3 && weekTrades.All(t => t.Pnl!.Value > 0);
            });

        // Max consecutive winning trades with 3:1+ R:R
        int maxSniperStreak = 0, currentSniperStreak = 0;
        foreach (var trade in closedTrades.OrderBy(t => t.ClosedDate ?? t.Date))
        {
            if (!trade.Pnl.HasValue || trade.Pnl.Value <= 0) { currentSniperStreak = 0; continue; }
            decimal risk = Math.Abs(trade.EntryPrice - trade.StopLoss);
            if (risk == 0) { currentSniperStreak = 0; continue; }
            decimal reward = Math.Abs(trade.TargetTier1 - trade.EntryPrice);
            if (reward / risk >= 3m) { currentSniperStreak++; maxSniperStreak = Math.Max(maxSniperStreak, currentSniperStreak); }
            else { currentSniperStreak = 0; }
        }

        // ICT Samurai: 25 winning ICT-complete trades with 2:1+ R:R
        int ictSamuraiCount = closedTrades.Count(t =>
            t.Pnl.HasValue && t.Pnl.Value > 0 &&
            t.PowerOf3Phase.HasValue && t.DailyBias.HasValue &&
            t.MarketStructure.HasValue && t.PremiumDiscount.HasValue &&
            Math.Abs(t.EntryPrice - t.StopLoss) > 0 &&
            Math.Abs(t.TargetTier1 - t.EntryPrice) / Math.Abs(t.EntryPrice - t.StopLoss) >= 2m);

        bool isIronman = closedCount >= 100 && winRate >= 60.0 && disciplinedStreak >= 50;
        bool isZenPerfection = bestWinStreak >= 10 && disciplinedStreak >= 100;
        bool isMarathonTrader = journalingStreak >= 365 && tradeCount >= 500;
        bool isEliteStatus = karmaLevel >= 20 && winRate >= 60.0 && closedCount >= 100;
        bool isLegendaryTrader = tradeCount >= 1000 && winRate >= 55.0 && karmaLevel >= 15;
        bool isPropFirmGod = existingAchievements.Contains(AchievementType.PropPhase1) &&
                             existingAchievements.Contains(AchievementType.PropPhase2) &&
                             existingAchievements.Contains(AchievementType.PropFundedReady);

        return new HardMetricsResult(
            hasPerfectWeek, maxSniperStreak, isIronman, ictSamuraiCount >= 25,
            isZenPerfection, isMarathonTrader, isEliteStatus, isLegendaryTrader, isPropFirmGod);
    }

    private sealed record HardMetricsResult(
        bool HasPerfectWeek, int MaxConsecutiveSniperRR3, bool IsIronman, bool IsIctSamurai,
        bool IsZenPerfection, bool IsMarathonTrader, bool IsEliteStatus,
        bool IsLegendaryTrader, bool IsPropFirmGod);

    // ── Inner Types ─────────────────────────────────────────────────────

    private sealed record AchievementDefinition(
        AchievementType Type,
        string Name,
        string Description,
        string Emoji,
        string Category,
        string? Medal = null);
}
