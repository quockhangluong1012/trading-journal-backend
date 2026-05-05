using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface ITradeProvider
{
    Task<List<TradeCacheDto>> GetTradesAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets trades for a user created on or after the specified date (for tilt detection).
    /// </summary>
    Task<List<TradeCacheDto>> GetRecentTradesAsync(int userId, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent N closed trades for a user, ordered by closed date descending (for streak tracking).
    /// </summary>
    Task<List<TradeCacheDto>> GetClosedTradesDescendingAsync(int userId, int count, CancellationToken cancellationToken = default);
}
