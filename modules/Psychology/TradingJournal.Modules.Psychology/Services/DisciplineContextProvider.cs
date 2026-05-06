using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.Psychology.Services;

internal sealed class DisciplineContextProvider(
    ITiltDetectionService tiltDetectionService) : IDisciplineContextProvider
{
    public async Task<DisciplineGuardianContextDto> GetDisciplineContextAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        TiltSnapshot snapshot = await tiltDetectionService.GetCurrentTiltAsync(userId, cancellationToken)
            ?? await tiltDetectionService.RecalculateTiltAsync(userId, cancellationToken);

        return new DisciplineGuardianContextDto(
            snapshot.Score,
            snapshot.Level.ToString(),
            snapshot.ConsecutiveLosses,
            snapshot.TradesLastHour,
            snapshot.RuleBreaksToday,
            snapshot.TodayPnl,
            snapshot.CooldownUntil);
    }
}