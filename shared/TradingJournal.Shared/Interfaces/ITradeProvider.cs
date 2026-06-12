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

    /// <summary>
    /// Gets closed trades in the specified date range, computed at the database level.
    /// </summary>
    Task<List<TradeCacheDto>> GetTradesInRangeAsync(int userId, DateTime fromDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pre-aggregated trade statistics (PnL, win/loss counts, open positions) computed at the database level.
    /// </summary>
    Task<TradeStatisticsDto> GetTradeStatisticsAsync(int userId, DateTime fromDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets per-asset PnL breakdown computed at the database level.
    /// </summary>
    Task<List<AssetBreakdownDto>> GetAssetBreakdownsAsync(int userId, DateTime fromDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets monthly PnL aggregates computed at the database level.
    /// </summary>
    Task<List<MonthlyPnlDto>> GetMonthlyPnlAsync(int userId, DateTime fromDate, CancellationToken cancellationToken = default);
}
