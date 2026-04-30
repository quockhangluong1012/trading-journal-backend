using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface ISetupProvider
{
    Task<List<SetupSummaryDto>> GetSetupsAsync(int userId, CancellationToken cancellationToken = default);
}
