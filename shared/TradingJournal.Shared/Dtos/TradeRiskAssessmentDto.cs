namespace TradingJournal.Shared.Dtos;

public sealed record TradeRiskAssessmentAlertDto(
    string Severity,
    string Message);

public sealed record TradeRiskAssessmentDto(
    decimal AccountBalance,
    decimal ConfiguredRiskPercent,
    decimal RecommendedRiskAmount,
    decimal SuggestedPositionUnits,
    decimal SuggestedPositionLots,
    decimal StopLossDistance,
    decimal RiskRewardRatio,
    int OpenPositionCount,
    int CorrelatedOpenPositionCount,
    bool IsCompliant,
    IReadOnlyList<TradeRiskAssessmentAlertDto> Alerts);

