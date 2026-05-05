using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Psychology.Events;
using TradingJournal.Shared.Contracts;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Psychology.Services;

/// <summary>
/// Computes win/loss streaks from closed trade data, persists snapshots,
/// and fires notifications at notable thresholds.
/// </summary>
public interface IStreakTrackingService
{
    /// <summary>
    /// Recalculates the current streak from trade data, persists a new StreakRecord,
    /// and fires a StreakAlertEvent if a notable threshold is reached.
    /// </summary>
    Task<StreakRecord> RecalculateStreakAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the latest streak record for a user without recalculating.
    /// </summary>
    Task<StreakRecord?> GetCurrentStreakAsync(int userId, CancellationToken ct = default);
}

internal sealed class StreakTrackingService(
    IPsychologyDbContext psychologyDb,
    ITradeProvider tradeProvider,
    IEventBus eventBus,
    ILogger<StreakTrackingService> logger) : IStreakTrackingService
{
    // Loss streak thresholds that trigger a notification (warning)
    private const int LossStreakWarningThreshold = 3;
    // Win streak thresholds that trigger a notification (celebration)
    private const int WinStreakCelebrationThreshold = 5;
    // How many closed trades to fetch for streak analysis
    private const int MaxTradesToAnalyze = 200;

    public async Task<StreakRecord?> GetCurrentStreakAsync(int userId, CancellationToken ct = default)
    {
        return await psychologyDb.StreakRecords
            .AsNoTracking()
            .Where(s => s.CreatedBy == userId)
            .OrderByDescending(s => s.RecordedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<StreakRecord> RecalculateStreakAsync(int userId, CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;

        // Fetch all closed trades ordered by close date descending
        var closedTrades = await tradeProvider.GetClosedTradesDescendingAsync(userId, MaxTradesToAnalyze, ct);

        // Calculate current streak from most recent trades
        var (streakType, streakLength, streakPnl) = CalculateCurrentStreak(closedTrades);

        // Calculate historical best/worst streaks by walking all trades chronologically
        var (bestWin, worstLoss) = CalculateHistoricalStreaks(closedTrades);

        // Ensure current streak is included in historical bests
        if (streakType == StreakType.Win && streakLength > bestWin)
            bestWin = streakLength;
        if (streakType == StreakType.Loss && streakLength > worstLoss)
            worstLoss = streakLength;

        // Check if this is a new personal record
        var previousRecord = await GetCurrentStreakAsync(userId, ct);
        bool isNewWinRecord = streakType == StreakType.Win && streakLength > (previousRecord?.BestWinStreak ?? 0);
        bool isNewLossRecord = streakType == StreakType.Loss && streakLength > (previousRecord?.WorstLossStreak ?? 0);

        // Persist the streak snapshot
        var record = new StreakRecord
        {
            Id = 0,
            StreakType = streakType,
            Length = streakLength,
            StreakPnl = streakPnl,
            BestWinStreak = bestWin,
            WorstLossStreak = worstLoss,
            TotalClosedTrades = closedTrades.Count,
            RecordedAt = now
        };

        psychologyDb.StreakRecords.Add(record);
        await psychologyDb.SaveChangesAsync(ct);

        logger.LogInformation(
            "Streak for user {UserId}: {Type} x{Length} (PnL: {PnL:F2}, BestWin: {BestWin}, WorstLoss: {WorstLoss})",
            userId, streakType, streakLength, streakPnl, bestWin, worstLoss);

        // Fire streak alert events at notable thresholds
        await TryFireStreakAlertAsync(userId, record, isNewWinRecord, isNewLossRecord, ct);

        return record;
    }

    private static (StreakType type, int length, decimal pnl) CalculateCurrentStreak(
        List<TradeCacheDto> closedTradesDescending)
    {
        if (closedTradesDescending.Count == 0)
            return (StreakType.None, 0, 0m);

        var firstTrade = closedTradesDescending[0];
        if (!firstTrade.Pnl.HasValue || firstTrade.Pnl.Value == 0)
            return (StreakType.None, 0, 0m);

        var streakType = firstTrade.Pnl.Value > 0 ? StreakType.Win : StreakType.Loss;
        int length = 0;
        decimal pnl = 0m;

        foreach (var trade in closedTradesDescending)
        {
            if (!trade.Pnl.HasValue)
                continue;

            // Breakeven trades (PnL == 0) break the streak
            if (trade.Pnl.Value == 0)
                break;

            bool isWin = trade.Pnl.Value > 0;
            bool matchesStreak = (streakType == StreakType.Win && isWin) ||
                                 (streakType == StreakType.Loss && !isWin);

            if (!matchesStreak)
                break;

            length++;
            pnl += trade.Pnl.Value;
        }

        return (streakType, length, pnl);
    }

    private static (int bestWin, int worstLoss) CalculateHistoricalStreaks(
        List<TradeCacheDto> closedTradesDescending)
    {
        if (closedTradesDescending.Count == 0)
            return (0, 0);

        // Walk chronologically (reverse of descending order)
        int bestWin = 0;
        int worstLoss = 0;
        int currentWinStreak = 0;
        int currentLossStreak = 0;

        for (int i = closedTradesDescending.Count - 1; i >= 0; i--)
        {
            var trade = closedTradesDescending[i];

            if (!trade.Pnl.HasValue || trade.Pnl.Value == 0)
            {
                // Breakeven resets both streaks
                bestWin = Math.Max(bestWin, currentWinStreak);
                worstLoss = Math.Max(worstLoss, currentLossStreak);
                currentWinStreak = 0;
                currentLossStreak = 0;
                continue;
            }

            if (trade.Pnl.Value > 0)
            {
                currentWinStreak++;
                worstLoss = Math.Max(worstLoss, currentLossStreak);
                currentLossStreak = 0;
            }
            else
            {
                currentLossStreak++;
                bestWin = Math.Max(bestWin, currentWinStreak);
                currentWinStreak = 0;
            }
        }

        // Final check for streaks at the end
        bestWin = Math.Max(bestWin, currentWinStreak);
        worstLoss = Math.Max(worstLoss, currentLossStreak);

        return (bestWin, worstLoss);
    }

    private async Task TryFireStreakAlertAsync(
        int userId, StreakRecord record, bool isNewWinRecord, bool isNewLossRecord, CancellationToken ct)
    {
        string? message = null;
        bool isNotable = false;

        if (record.StreakType == StreakType.Loss && record.Length >= LossStreakWarningThreshold)
        {
            isNotable = true;
            message = isNewLossRecord
                ? $"⚠️ New worst losing streak: {record.Length} consecutive losses ({record.StreakPnl:F2}). Strongly consider pausing."
                : $"⚠️ {record.Length} consecutive losses ({record.StreakPnl:F2}). Consider stepping back to reset.";
        }
        else if (record.StreakType == StreakType.Win && record.Length >= WinStreakCelebrationThreshold)
        {
            isNotable = true;
            message = isNewWinRecord
                ? $"🔥 New personal record! {record.Length} consecutive wins (+{record.StreakPnl:F2}). Stay disciplined!"
                : $"🔥 {record.Length} consecutive wins (+{record.StreakPnl:F2}). Keep your edge — don't over-leverage!";
        }

        if (isNotable && message != null)
        {
            // Check if we already fired a streak alert recently (within 5 minutes) to avoid spam
            bool recentlyFired = await psychologyDb.StreakRecords
                .AsNoTracking()
                .Where(s => s.CreatedBy == userId
                    && s.Id != record.Id
                    && s.RecordedAt >= DateTime.UtcNow.AddMinutes(-5))
                .AnyAsync(ct);

            if (!recentlyFired)
            {
                logger.LogInformation("🎯 Streak alert for user {UserId}: {Message}", userId, message);

                await eventBus.PublishAsync(new StreakAlertEvent(
                    EventId: Guid.NewGuid(),
                    UserId: userId,
                    StreakType: record.StreakType.ToString(),
                    StreakLength: record.Length,
                    StreakPnl: record.StreakPnl,
                    IsNewRecord: isNewWinRecord || isNewLossRecord,
                    Message: message), ct);
            }
        }
    }
}
