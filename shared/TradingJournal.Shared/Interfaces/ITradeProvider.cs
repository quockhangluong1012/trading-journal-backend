using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface ITradeProvider
{
    Task<List<TradeCacheDto>> GetTradesAsync(CancellationToken cancellationToken = default);
}
