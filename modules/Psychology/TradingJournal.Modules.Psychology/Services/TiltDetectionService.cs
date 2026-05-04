using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Psychology.Events;
using TradingJournal.Shared.Contracts;

namespace TradingJournal.Modules.Psychology.Services;

/// <summary>
/// Calculates tilt scores based on recent trading behavior.
/// Algorithm weights:
///   - Consecutive losses: 35% (0-35 pts)
///   - Trade frequency spike: 20% (0-20 pts)
///   - Rule breaks today: 20% (0-20 pts)
///   - Daily PnL drawdown: 15% (0-15 pts)
///   - Time-of-day risk: 10% (0-10 pts — late-night trading penalty)
/// </summary>
public interface ITiltDetectionService
{
    /// <summary>
    /// Recalculates the tilt score for a user based on their recent trading data,
    /// persists a TiltSnapshot, and fires a circuit breaker event if score exceeds threshold.
    /// </summary>
    Task<TiltSnapshot> RecalculateTiltAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the latest tilt snapshot for a user without recalculating.
    /// </summary>
    Task<TiltSnapshot?> GetCurrentTiltAsync(int userId, CancellationToken ct = default);
}

internal sealed class TiltDetectionService(
    IPsychologyDbContext psychologyDb,
    ITradeProvider tradeProvider,
    IEventBus eventBus,
    ILogger<TiltDetectionService> logger) : ITiltDetectionService
{
    // Circuit breaker fires at this threshold
    private const int CircuitBreakerThreshold = 70;
    private const int CooldownMinutes = 30;

    // Score weights
    private const double ConsecutiveLossWeight = 35.0;
    private const double TradeFrequencyWeight = 20.0;
    private const double RuleBreakWeight = 20.0;
    private const double PnlDrawdownWeight = 15.0;
    private const double TimeOfDayWeight = 10.0;

    // Thresholds
    private const int MaxConsecutiveLossesForFullScore = 5;
    private const int MaxTradesPerHourForFullScore = 6;
    private const int MaxRuleBreaksForFullScore = 3;
    private const decimal MaxDailyDrawdownForFullScore = -500m;

    public async Task<TiltSnapshot?> GetCurrentTiltAsync(int userId, CancellationToken ct = default)
    {
        return await psychologyDb.TiltSnapshots
            .AsNoTracking()
            .Where(t => t.CreatedBy == userId)
            .OrderByDescending(t => t.RecordedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TiltSnapshot> RecalculateTiltAsync(int userId, CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;
        DateTime todayStart = now.Date;
        DateTime oneHourAgo = now.AddHours(-1);

        // Fetch recent trades for this user (today's trades for most metrics)
        var recentTrades = await tradeProvider.GetRecentTradesAsync(userId, todayStart, ct);

        // 1. Calculate consecutive losses (from most recent trades, regardless of date)
        var allClosedTrades = await tradeProvider.GetClosedTradesDescendingAsync(userId, 20, ct);
        int consecutiveLosses = 0;
        int consecutiveWins = 0;
        bool countingLosses = true;
        bool countingWins = true;

        foreach (var trade in allClosedTrades)
        {
            if (countingLosses && trade.Pnl < 0)
                consecutiveLosses++;
            else
                countingLosses = false;

            if (countingWins && trade.Pnl > 0)
                consecutiveWins++;
            else
                countingWins = false;

            if (!countingLosses && !countingWins)
                break;
        }

        // If most recent trade is a loss, wins = 0 and vice versa
        if (allClosedTrades.Count > 0)
        {
            if (allClosedTrades[0].Pnl < 0)
                consecutiveWins = 0;
            else
                consecutiveLosses = 0;
        }

        // 2. Trade frequency in last hour
        int tradesLastHour = recentTrades.Count(t => t.Date >= oneHourAgo);

        // 3. Rule breaks today
        int ruleBreaksToday = recentTrades.Count(t => t.IsRuleBroken);

        // 4. Today's PnL
        decimal todayPnl = recentTrades.Where(t => t.Pnl.HasValue).Sum(t => t.Pnl!.Value);

        // 5. Time-of-day risk (late night trading penalty: 10 PM - 4 AM user local, approximate with UTC)
        int hourUtc = now.Hour;
        bool isLateNight = hourUtc is >= 22 or < 4;

        // Calculate component scores
        double lossScore = Math.Min(1.0, (double)consecutiveLosses / MaxConsecutiveLossesForFullScore) * ConsecutiveLossWeight;
        double frequencyScore = Math.Min(1.0, (double)tradesLastHour / MaxTradesPerHourForFullScore) * TradeFrequencyWeight;
        double ruleBreakScore = Math.Min(1.0, (double)ruleBreaksToday / MaxRuleBreaksForFullScore) * RuleBreakWeight;
        double pnlScore = todayPnl < 0
            ? Math.Min(1.0, (double)(Math.Abs(todayPnl) / Math.Abs(MaxDailyDrawdownForFullScore))) * PnlDrawdownWeight
            : 0;
        double timeScore = isLateNight ? TimeOfDayWeight : 0;

        int totalScore = (int)Math.Round(lossScore + frequencyScore + ruleBreakScore + pnlScore + timeScore);
        totalScore = Math.Clamp(totalScore, 0, 100);

        TiltLevel level = totalScore switch
        {
            <= 20 => TiltLevel.Calm,
            <= 40 => TiltLevel.Elevated,
            <= 60 => TiltLevel.Warning,
            <= 80 => TiltLevel.High,
            _ => TiltLevel.Critical
        };

        bool circuitBreakerTriggered = totalScore >= CircuitBreakerThreshold;
        DateTime? cooldownUntil = circuitBreakerTriggered ? now.AddMinutes(CooldownMinutes) : null;

        // Persist snapshot
        var snapshot = new TiltSnapshot
        {
            Id = 0,
            Score = totalScore,
            ConsecutiveLosses = consecutiveLosses,
            ConsecutiveWins = consecutiveWins,
            TradesLastHour = tradesLastHour,
            RuleBreaksToday = ruleBreaksToday,
            TodayPnl = todayPnl,
            Level = level,
            CircuitBreakerTriggered = circuitBreakerTriggered,
            CooldownUntil = cooldownUntil,
            RecordedAt = now
        };

        psychologyDb.TiltSnapshots.Add(snapshot);
        await psychologyDb.SaveChangesAsync(ct);

        logger.LogInformation(
            "Tilt score for user {UserId}: {Score}/100 (Level: {Level}, Losses: {Losses}, Freq: {Freq}, Rules: {Rules}, PnL: {PnL})",
            userId, totalScore, level, consecutiveLosses, tradesLastHour, ruleBreaksToday, todayPnl);

        // Fire circuit breaker event if threshold exceeded
        if (circuitBreakerTriggered)
        {
            // Check if we already fired a circuit breaker in the last cooldown period
            bool alreadyFired = await psychologyDb.TiltSnapshots
                .AsNoTracking()
                .Where(t => t.CreatedBy == userId
                    && t.CircuitBreakerTriggered
                    && t.Id != snapshot.Id
                    && t.RecordedAt >= now.AddMinutes(-CooldownMinutes))
                .AnyAsync(ct);

            if (!alreadyFired)
            {
                logger.LogWarning("🚨 Circuit breaker triggered for user {UserId}! Tilt score: {Score}", userId, totalScore);

                await eventBus.PublishAsync(new TiltCircuitBreakerEvent(
                    EventId: Guid.NewGuid(),
                    UserId: userId,
                    TiltScore: totalScore,
                    TiltLevel: level.ToString(),
                    ConsecutiveLosses: consecutiveLosses,
                    TradesLastHour: tradesLastHour,
                    RuleBreaksToday: ruleBreaksToday,
                    TodayPnl: todayPnl,
                    CooldownUntil: cooldownUntil!.Value), ct);
            }
        }

        return snapshot;
    }
}
