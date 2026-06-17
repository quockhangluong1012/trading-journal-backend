using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace TradingJournal.Modules.Backtest.Infrastructure;

public interface IBacktestDbContext
{
    /// <summary>
    /// Change tracker for the underlying context. Exposed so bulk-insert paths can
    /// detach already-persisted entities and keep the tracker (and memory) bounded.
    /// </summary>
    ChangeTracker ChangeTracker { get; }

    DbSet<BacktestSession> BacktestSessions { get; set; }

    DbSet<BacktestOrder> BacktestOrders { get; set; }

    DbSet<BacktestTradeResult> BacktestTradeResults { get; set; }

    DbSet<OhlcvCandle> OhlcvCandles { get; set; }

    DbSet<ChartDrawing> ChartDrawings { get; set; }

    DbSet<ChartDrawingTemplate> ChartDrawingTemplates { get; set; }

    DbSet<BacktestAsset> BacktestAssets { get; set; }

    DbSet<CsvImportJob> CsvImportJobs { get; set; }

    Task BeginTransaction();

    Task CommitTransaction();

    Task RollbackTransaction();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
