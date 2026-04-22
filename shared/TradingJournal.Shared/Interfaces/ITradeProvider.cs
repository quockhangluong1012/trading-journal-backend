using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface ITradeProvider
{
    Task<List<TradeCacheDto>> GetTradesAsync(int userId, CancellationToken cancellationToken = default);
}
