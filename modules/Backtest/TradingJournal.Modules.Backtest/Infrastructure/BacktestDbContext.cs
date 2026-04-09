using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Backtest.Infrastructure;

internal sealed class BacktestDbContext(DbContextOptions<BacktestDbContext> options, IHttpContextAccessor httpContextAccessor)
    : DbContext(options), IBacktestDbContext
{
    private IDbContextTransaction? _transaction;

    public DbSet<BacktestSession> BacktestSessions { get; set; } = null!;

    public DbSet<BacktestOrder> BacktestOrders { get; set; } = null!;

    public DbSet<BacktestTradeResult> BacktestTradeResults { get; set; } = null!;

    public DbSet<OhlcvCandle> OhlcvCandles { get; set; } = null!;

    public DbSet<ChartDrawing> ChartDrawings { get; set; } = null!;

    public DbSet<BacktestAsset> BacktestAssets { get; set; } = null!;

    public DbSet<CsvImportJob> CsvImportJobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BacktestSession>(builder =>
        {
            builder.ToTable("BacktestSessions", "Backtest");

            builder.Property(s => s.InitialBalance).HasColumnType("decimal(18,2)");
            builder.Property(s => s.CurrentBalance).HasColumnType("decimal(18,2)");
            builder.Property(s => s.Spread).HasColumnType("decimal(28,10)").HasDefaultValue(0m);

            builder.HasMany(s => s.Orders)
                .WithOne(o => o.Session)
                .HasForeignKey(o => o.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(s => s.TradeResults)
                .WithOne(t => t.Session)
                .HasForeignKey(t => t.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BacktestOrder>(builder =>
        {
            builder.ToTable("BacktestOrders", "Backtest");

            builder.Property(o => o.EntryPrice).HasColumnType("decimal(28,10)");
            builder.Property(o => o.FilledPrice).HasColumnType("decimal(28,10)");
            builder.Property(o => o.PositionSize).HasColumnType("decimal(18,8)");
            builder.Property(o => o.StopLoss).HasColumnType("decimal(28,10)");
            builder.Property(o => o.TakeProfit).HasColumnType("decimal(28,10)");
            builder.Property(o => o.ExitPrice).HasColumnType("decimal(28,10)");
            builder.Property(o => o.Pnl).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<BacktestTradeResult>(builder =>
        {
            builder.ToTable("BacktestTradeResults", "Backtest");

            builder.Property(t => t.EntryPrice).HasColumnType("decimal(28,10)");
            builder.Property(t => t.ExitPrice).HasColumnType("decimal(28,10)");
            builder.Property(t => t.PositionSize).HasColumnType("decimal(18,8)");
            builder.Property(t => t.Pnl).HasColumnType("decimal(18,4)");
            builder.Property(t => t.BalanceAfter).HasColumnType("decimal(18,2)");

            builder.HasOne(t => t.Order)
                .WithMany()
                .HasForeignKey(t => t.OrderId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<OhlcvCandle>(builder =>
        {
            builder.ToTable("OhlcvCandles", "Backtest");

            builder.HasIndex(c => new { c.Asset, c.Timeframe, c.Timestamp })
                .IsUnique()
                .HasDatabaseName("IX_OhlcvCandles_AssetTimeframeTimestamp");
        });

        modelBuilder.Entity<ChartDrawing>(builder =>
        {
            builder.ToTable("ChartDrawings", "Backtest");

            builder.HasIndex(d => d.SessionId)
                .IsUnique()
                .HasDatabaseName("IX_ChartDrawings_SessionId");
        });

        modelBuilder.Entity<BacktestAsset>(builder =>
        {
            builder.ToTable("BacktestAssets", "Backtest");

            builder.HasIndex(a => a.Symbol)
                .IsUnique()
                .HasDatabaseName("IX_BacktestAssets_Symbol");

            builder.Property(a => a.DefaultSpreadPips).HasColumnType("decimal(18,4)").HasDefaultValue(0m);
            builder.Property(a => a.PipSize).HasColumnType("decimal(18,10)").HasDefaultValue(0.0001m);
        });

        modelBuilder.Entity<CsvImportJob>(builder =>
        {
            builder.ToTable("CsvImportJobs", "Backtest");

            builder.HasIndex(j => new { j.Status, j.CreatedDate })
                .HasDatabaseName("IX_CsvImportJobs_StatusCreated");

            builder.HasOne(j => j.Asset)
                .WithMany()
                .HasForeignKey(j => j.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public async Task BeginTransaction()
    {
        _transaction = await Database.BeginTransactionAsync();
    }

    public async Task CommitTransaction()
    {
        if (_transaction == null) return;
        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransaction()
    {
        if (_transaction == null) return;
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;

        foreach (EntityEntry<EntityBase<int>> entry in ChangeTracker.Entries<EntityBase<int>>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedDate = DateTime.UtcNow;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedDate = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
