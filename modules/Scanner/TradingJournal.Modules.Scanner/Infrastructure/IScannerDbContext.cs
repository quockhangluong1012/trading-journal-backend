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

    Task BeginTransaction();

    Task CommitTransaction();

    Task RollbackTransaction();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
