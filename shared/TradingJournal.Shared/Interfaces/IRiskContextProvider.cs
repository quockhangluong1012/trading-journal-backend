using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface IRiskContextProvider
{
    Task<RiskAdvisorContextDto> GetRiskContextAsync(int userId, CancellationToken cancellationToken = default);
}