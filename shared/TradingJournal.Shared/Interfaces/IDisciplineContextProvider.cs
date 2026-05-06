using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface IDisciplineContextProvider
{
    Task<DisciplineGuardianContextDto> GetDisciplineContextAsync(int userId, CancellationToken cancellationToken = default);
}