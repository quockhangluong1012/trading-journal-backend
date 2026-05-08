namespace TradingJournal.Modules.Scanner.Infrastructure;

public interface IScannerDbContext
{
    DbSet<Watchlist> Watchlists { get; set; }

    DbSet<WatchlistAsset> WatchlistAssets { get; set; }

    DbSet<WatchlistAssetDetector> WatchlistAssetDetectors { get; set; }

    DbSet<ScannerAlert> ScannerAlerts { get; set; }

    DbSet<ScannerConfig> ScannerConfigs { get; set; }

    DbSet<ScannerConfigPattern> ScannerConfigPatterns { get; set; }

    DbSet<ScannerConfigTimeframe> ScannerConfigTimeframes { get; set; }

    [Obsolete("Manual transactions are not retry-safe with execution strategies. Wrap the full unit of work in Database.CreateExecutionStrategy().ExecuteAsync instead.")]
    Task BeginTransaction();

    [Obsolete("Manual transactions are not retry-safe with execution strategies. Wrap the full unit of work in Database.CreateExecutionStrategy().ExecuteAsync instead.")]
    Task CommitTransaction();

    [Obsolete("Manual transactions are not retry-safe with execution strategies. Wrap the full unit of work in Database.CreateExecutionStrategy().ExecuteAsync instead.")]
    Task RollbackTransaction();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
