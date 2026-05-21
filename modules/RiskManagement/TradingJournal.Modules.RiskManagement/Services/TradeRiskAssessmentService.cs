using TradingJournal.Modules.RiskManagement.Common.Helpers;
using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Modules.RiskManagement.Services;

internal sealed class TradeRiskAssessmentService(
    IRiskDbContext context,
    ITradeProvider tradeProvider) : ITradeRiskAssessmentService
{
    private const decimal HighCorrelationThreshold = 0.7m;

    public async Task<TradeRiskAssessmentDto> AssessAsync(
        int userId,
        string asset,
        decimal entryPrice,
        decimal stopLossPrice,
        decimal targetPrice,
        TradeStatus tradeStatus,
        int? existingTradeId = null,
        CancellationToken cancellationToken = default)
    {
        RiskConfig? config = await context.RiskConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CreatedBy == userId, cancellationToken);

        decimal accountBalance = config?.AccountBalance ?? 10000m;
        decimal configuredRiskPercent = config?.RiskPerTradePercent ?? 1.0m;
        int maxOpenPositions = config?.MaxOpenPositions ?? 5;
        int maxCorrelatedPositions = config?.MaxCorrelatedPositions ?? 3;

        var sizing = PositionSizingCalculator.Calculate(accountBalance, configuredRiskPercent, entryPrice, stopLossPrice);
        decimal stopLossDistance = Math.Abs(entryPrice - stopLossPrice);
        decimal rewardDistance = Math.Abs(targetPrice - entryPrice);
        decimal riskRewardRatio = stopLossDistance > 0 ? rewardDistance / stopLossDistance : 0m;

        List<TradeCacheDto> allTrades = await tradeProvider.GetTradesAsync(userId, cancellationToken);
        List<TradeCacheDto> relevantTrades = existingTradeId.HasValue
            ? [.. allTrades.Where(t => t.Id != existingTradeId.Value)]
            : allTrades;

        DateTime today = DateTime.UtcNow.Date;
        DateTime weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        if (today.DayOfWeek == DayOfWeek.Sunday)
        {
            weekStart = weekStart.AddDays(-7);
        }

        List<TradeCacheDto> todayTrades = [.. relevantTrades
            .Where(t => t.ClosedDate.HasValue && t.ClosedDate.Value.Date == today && t.Status == TradeStatus.Closed)];

        List<TradeCacheDto> weekTrades = [.. relevantTrades
            .Where(t => t.ClosedDate.HasValue && t.ClosedDate.Value.Date >= weekStart && t.Status == TradeStatus.Closed)];

        List<TradeCacheDto> openTrades = [.. relevantTrades.Where(t => t.Status == TradeStatus.Open)];

        int projectedOpenPositionCount = openTrades.Count + (tradeStatus == TradeStatus.Open ? 1 : 0);
        int correlatedOpenPositionCount = openTrades.Count(t => CorrelationData.GetCorrelation(asset, t.Asset) >= HighCorrelationThreshold);
        int projectedCorrelatedOpenPositionCount = correlatedOpenPositionCount + (tradeStatus == TradeStatus.Open ? 1 : 0);

        decimal dailyLimitPct = config?.DailyLossLimitPercent ?? 2.0m;
        decimal weeklyCapPct = config?.WeeklyDrawdownCapPercent ?? 5.0m;
        decimal dailyPnl = todayTrades.Sum(t => t.Pnl ?? 0);
        decimal weeklyPnl = weekTrades.Sum(t => t.Pnl ?? 0);
        decimal dailyLimitMax = accountBalance * (dailyLimitPct / 100m);
        decimal weeklyCapMax = accountBalance * (weeklyCapPct / 100m);
        decimal dailyLimitUsed = dailyLimitMax > 0 ? Math.Abs(Math.Min(dailyPnl, 0)) / dailyLimitMax * 100 : 0;
        decimal weeklyCapUsed = weeklyCapMax > 0 ? Math.Abs(Math.Min(weeklyPnl, 0)) / weeklyCapMax * 100 : 0;

        List<TradeRiskAssessmentAlertDto> alerts = [];

        if (tradeStatus == TradeStatus.Open && projectedOpenPositionCount > maxOpenPositions)
        {
            alerts.Add(new("critical", $"Projected open positions ({projectedOpenPositionCount}) exceed configured max ({maxOpenPositions})."));
        }
        else if (tradeStatus == TradeStatus.Open && projectedOpenPositionCount == maxOpenPositions)
        {
            alerts.Add(new("warning", $"This trade would bring you to your max open positions limit ({maxOpenPositions})."));
        }

        if (tradeStatus == TradeStatus.Open && projectedCorrelatedOpenPositionCount > maxCorrelatedPositions)
        {
            alerts.Add(new("critical", $"Projected correlated exposure ({projectedCorrelatedOpenPositionCount}) exceeds configured max ({maxCorrelatedPositions})."));
        }
        else if (tradeStatus == TradeStatus.Open && projectedCorrelatedOpenPositionCount == maxCorrelatedPositions)
        {
            alerts.Add(new("warning", $"This trade would bring correlated exposure to the configured limit ({maxCorrelatedPositions})."));
        }

        if (dailyLimitUsed >= 100)
        {
            alerts.Add(new("critical", $"Daily loss limit already breached ({dailyLimitPct:F2}%)."));
        }
        else if (dailyLimitUsed >= 75)
        {
            alerts.Add(new("warning", $"Daily loss limit usage is already elevated at {dailyLimitUsed:F0}%."));
        }

        if (weeklyCapUsed >= 100)
        {
            alerts.Add(new("critical", $"Weekly drawdown cap already breached ({weeklyCapPct:F2}%)."));
        }
        else if (weeklyCapUsed >= 75)
        {
            alerts.Add(new("warning", $"Weekly drawdown cap usage is already elevated at {weeklyCapUsed:F0}%."));
        }

        if (riskRewardRatio < 1m)
        {
            alerts.Add(new("warning", $"Planned risk-reward ratio is below 1.0 ({riskRewardRatio:F2})."));
        }

        bool isCompliant = alerts.All(alert => alert.Severity != "critical");

        return new TradeRiskAssessmentDto(
            Math.Round(accountBalance, 2),
            configuredRiskPercent,
            Math.Round(sizing.RiskAmount, 2),
            sizing.Units,
            sizing.Lots,
            Math.Round(stopLossDistance, 5),
            Math.Round(riskRewardRatio, 2),
            projectedOpenPositionCount,
            projectedCorrelatedOpenPositionCount,
            isCompliant,
            alerts);
    }
}
