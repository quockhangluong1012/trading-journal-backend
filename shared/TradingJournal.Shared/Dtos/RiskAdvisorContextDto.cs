namespace TradingJournal.Shared.Dtos;

public sealed record RiskAdvisorAlertDto(
    string Severity,
    string Title,
    string Message);

public sealed record RiskAdvisorContextDto(
    decimal AccountBalance,
    decimal DailyLossLimitPercent,
    decimal WeeklyDrawdownCapPercent,
    int MaxOpenPositions,
    decimal DailyPnl,
    decimal DailyPnlPercent,
    decimal WeeklyPnl,
    decimal WeeklyPnlPercent,
    int TodayTradeCount,
    int OpenPositionCount,
    int WeekTradeCount,
    int TodayWins,
    int TodayLosses,
    decimal DailyLimitUsedPercent,
    decimal WeeklyCapUsedPercent,
    bool IsDailyLimitBreached,
    bool IsWeeklyCapBreached,
    IReadOnlyList<RiskAdvisorAlertDto> Alerts);