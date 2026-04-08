namespace TradingJournal.Modules.Backtest.Infrastructure;

public interface IBacktestDbContext
{
    DbSet<BacktestSession> BacktestSessions { get; set; }

    DbSet<BacktestOrder> BacktestOrders { get; set; }

    DbSet<BacktestTradeResult> BacktestTradeResults { get; set; }

    DbSet<OhlcvCandle> OhlcvCandles { get; set; }

    DbSet<ChartDrawing> ChartDrawings { get; set; }

    DbSet<BacktestAsset> BacktestAssets { get; set; }

    Task BeginTransaction();

    Task CommitTransaction();

    Task RollbackTransaction();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
