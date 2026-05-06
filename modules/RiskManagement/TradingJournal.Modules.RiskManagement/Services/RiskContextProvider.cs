using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.RiskManagement.Services;

internal sealed class RiskContextProvider(
    IRiskDbContext context,
    ITradeProvider tradeProvider) : IRiskContextProvider
{
    public async Task<RiskAdvisorContextDto> GetRiskContextAsync(int userId, CancellationToken cancellationToken = default)
    {
        RiskConfig? config = await context.RiskConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CreatedBy == userId, cancellationToken);

        decimal accountBalance = config?.AccountBalance ?? 10000m;
        decimal dailyLimitPct = config?.DailyLossLimitPercent ?? 2.0m;
        decimal weeklyCapPct = config?.WeeklyDrawdownCapPercent ?? 5.0m;
        int maxOpenPositions = config?.MaxOpenPositions ?? 5;

        List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(userId, cancellationToken);

        DateTime today = DateTime.UtcNow.Date;
        DateTime weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        if (today.DayOfWeek == DayOfWeek.Sunday)
        {
            weekStart = weekStart.AddDays(-7);
        }

        List<TradeCacheDto> todayTrades = [.. allTrades
            .Where(t => t.ClosedDate.HasValue && t.ClosedDate.Value.Date == today && t.Status == TradeStatus.Closed)];

        List<TradeCacheDto> weekTrades = [.. allTrades
            .Where(t => t.ClosedDate.HasValue && t.ClosedDate.Value.Date >= weekStart && t.Status == TradeStatus.Closed)];

        int openPositionCount = allTrades.Count(t => t.Status == TradeStatus.Open);

        decimal dailyPnl = todayTrades.Sum(t => t.Pnl ?? 0);
        decimal weeklyPnl = weekTrades.Sum(t => t.Pnl ?? 0);

        decimal dailyPnlPct = accountBalance > 0 ? (dailyPnl / accountBalance) * 100 : 0;
        decimal weeklyPnlPct = accountBalance > 0 ? (weeklyPnl / accountBalance) * 100 : 0;

        decimal dailyLimitMax = accountBalance * (dailyLimitPct / 100m);
        decimal weeklyCapMax = accountBalance * (weeklyCapPct / 100m);

        decimal dailyLoss = Math.Min(dailyPnl, 0);
        decimal weeklyLoss = Math.Min(weeklyPnl, 0);

        decimal dailyLimitUsed = dailyLimitMax > 0 ? Math.Abs(dailyLoss) / dailyLimitMax * 100 : 0;
        decimal weeklyCapUsed = weeklyCapMax > 0 ? Math.Abs(weeklyLoss) / weeklyCapMax * 100 : 0;

        bool isDailyBreached = dailyLimitUsed >= 100;
        bool isWeeklyBreached = weeklyCapUsed >= 100;

        int todayWins = todayTrades.Count(t => t.Pnl > 0);
        int todayLosses = todayTrades.Count(t => t.Pnl < 0);

        List<RiskAdvisorAlertDto> alerts = [];

        if (isDailyBreached)
        {
            alerts.Add(new RiskAdvisorAlertDto(
                "critical",
                "Daily Loss Limit Breached",
                $"You have exceeded your daily loss limit of {dailyLimitPct}%. Consider stopping trading for today."));
        }
        else if (dailyLimitUsed >= 75)
        {
            alerts.Add(new RiskAdvisorAlertDto(
                "warning",
                "Approaching Daily Loss Limit",
                $"You have used {dailyLimitUsed:F0}% of your daily loss limit. Trade with caution."));
        }

        if (isWeeklyBreached)
        {
            alerts.Add(new RiskAdvisorAlertDto(
                "critical",
                "Weekly Drawdown Cap Breached",
                $"You have exceeded your weekly drawdown cap of {weeklyCapPct}%. Strongly consider taking a break."));
        }
        else if (weeklyCapUsed >= 75)
        {
            alerts.Add(new RiskAdvisorAlertDto(
                "warning",
                "Approaching Weekly Drawdown Cap",
                $"You have used {weeklyCapUsed:F0}% of your weekly drawdown cap."));
        }

        if (openPositionCount >= maxOpenPositions)
        {
            alerts.Add(new RiskAdvisorAlertDto(
                "warning",
                "Max Open Positions Reached",
                $"You have {openPositionCount} open positions (limit: {maxOpenPositions}). Close existing positions before opening new ones."));
        }

        if (todayLosses >= 3 && todayWins == 0)
        {
            alerts.Add(new RiskAdvisorAlertDto(
                "warning",
                "Losing Streak Detected",
                $"You have {todayLosses} consecutive losses today with no wins. Consider taking a break."));
        }

        return new RiskAdvisorContextDto(
            Math.Round(accountBalance, 2),
            dailyLimitPct,
            weeklyCapPct,
            maxOpenPositions,
            Math.Round(dailyPnl, 2),
            Math.Round(dailyPnlPct, 2),
            Math.Round(weeklyPnl, 2),
            Math.Round(weeklyPnlPct, 2),
            todayTrades.Count,
            openPositionCount,
            weekTrades.Count,
            todayWins,
            todayLosses,
            Math.Round(Math.Min(dailyLimitUsed, 100), 2),
            Math.Round(Math.Min(weeklyCapUsed, 100), 2),
            isDailyBreached,
            isWeeklyBreached,
            alerts);
    }
}