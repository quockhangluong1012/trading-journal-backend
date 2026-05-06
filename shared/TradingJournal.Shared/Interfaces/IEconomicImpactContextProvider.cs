using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface IEconomicImpactContextProvider
{
    Task<EconomicImpactContextDto> GetEconomicImpactContextAsync(
        int userId,
        string symbol,
        int proximityMinutes,
        CancellationToken cancellationToken = default);
}