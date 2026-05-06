namespace TradingJournal.Shared.Dtos;

public sealed record DisciplineGuardianContextDto(
    int TiltScore,
    string TiltLevel,
    int ConsecutiveLosses,
    int TradesLastHour,
    int RuleBreaksToday,
    decimal TodayPnl,
    DateTime? CooldownUntil);