using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.Scanner.Infrastructure;

internal sealed class ScannerDbContext(
    DbContextOptions<ScannerDbContext> options,
    IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), IScannerDbContext
{
    public DbSet<Watchlist> Watchlists { get; set; } = null!;

    public DbSet<WatchlistAsset> WatchlistAssets { get; set; } = null!;

    public DbSet<WatchlistAssetDetector> WatchlistAssetDetectors { get; set; } = null!;

    public DbSet<ScannerAlert> ScannerAlerts { get; set; } = null!;

    public DbSet<ScannerConfig> ScannerConfigs { get; set; } = null!;

    public DbSet<ScannerConfigPattern> ScannerConfigPatterns { get; set; } = null!;

    public DbSet<ScannerConfigTimeframe> ScannerConfigTimeframes { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Watchlist>(builder =>
        {
            builder.ToTable("Watchlists", "Scanner");

            builder.HasMany(w => w.Assets)
                .WithOne(a => a.Watchlist)
                .HasForeignKey(a => a.WatchlistId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(w => w.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<WatchlistAsset>(builder =>
        {
            builder.ToTable("WatchlistAssets", "Scanner");

            builder.HasIndex(a => new { a.WatchlistId, a.Symbol })
                .IsUnique()
                .HasDatabaseName("IX_WatchlistAssets_WatchlistSymbol");

            builder.Property(a => a.Symbol).HasMaxLength(30);
            builder.Property(a => a.DisplayName).HasMaxLength(100);

            builder.HasMany(a => a.EnabledDetectors)
                .WithOne(d => d.WatchlistAsset)
                .HasForeignKey(d => d.WatchlistAssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WatchlistAssetDetector>(builder =>
        {
            builder.ToTable("WatchlistAssetDetectors", "Scanner");

            builder.HasIndex(d => new { d.WatchlistAssetId, d.PatternType })
                .IsUnique()
                .HasDatabaseName("IX_WatchlistAssetDetectors_AssetPattern");
        });

        modelBuilder.Entity<ScannerAlert>(builder =>
        {
            builder.ToTable("ScannerAlerts", "Scanner");

            builder.HasIndex(a => new { a.UserId, a.DetectedAt })
                .HasDatabaseName("IX_ScannerAlerts_UserDetectedAt");

            builder.HasIndex(a => new { a.UserId, a.Symbol, a.PatternType, a.DetectionTimeframe, a.DetectedAt })
                .HasDatabaseName("IX_ScannerAlerts_Dedup");

            builder.Property(a => a.Symbol).HasMaxLength(30);
            builder.Property(a => a.Description).HasMaxLength(500);
            builder.Property(a => a.PriceAtDetection).HasColumnType("decimal(28,10)");
            builder.Property(a => a.ZoneHighPrice).HasColumnType("decimal(28,10)");
            builder.Property(a => a.ZoneLowPrice).HasColumnType("decimal(28,10)");
        });

        modelBuilder.Entity<ScannerConfig>(builder =>
        {
            builder.ToTable("ScannerConfigs", "Scanner");

            builder.HasIndex(c => c.UserId)
                .IsUnique()
                .HasDatabaseName("IX_ScannerConfigs_UserId");

            builder.HasMany(c => c.EnabledPatterns)
                .WithOne(p => p.ScannerConfig)
                .HasForeignKey(p => p.ScannerConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(c => c.EnabledTimeframes)
                .WithOne(t => t.ScannerConfig)
                .HasForeignKey(t => t.ScannerConfigId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScannerConfigPattern>(builder =>
        {
            builder.ToTable("ScannerConfigPatterns", "Scanner");

            builder.HasIndex(p => new { p.ScannerConfigId, p.PatternType })
                .IsUnique()
                .HasDatabaseName("IX_ScannerConfigPatterns_ConfigPattern");
        });

        modelBuilder.Entity<ScannerConfigTimeframe>(builder =>
        {
            builder.ToTable("ScannerConfigTimeframes", "Scanner");

            builder.HasIndex(t => new { t.ScannerConfigId, t.Timeframe })
                .IsUnique()
                .HasDatabaseName("IX_ScannerConfigTimeframes_ConfigTimeframe");
        });
    }
}
