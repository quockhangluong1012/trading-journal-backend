using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades.Infrastructure;

internal sealed class TradeDbContext(DbContextOptions<TradeDbContext> options, IHttpContextAccessor httpContextAccessor)
    : DbContext(options), ITradeDbContext
{
    private IDbContextTransaction? _transaction;

    public DbSet<TradeHistory> TradeHistories { get; set; } = null!;

    public DbSet<ChecklistModel> ChecklistModels { get; set; } = null!;

    public DbSet<PretradeChecklist> PretradeChecklists { get; set; } = null!;

    public DbSet<TradeScreenShot> TradeScreenShots { get; set; } = null!;

    public DbSet<TradingZone> TradingZones { get; set; } = null!;

    public DbSet<TradingSession> TradingSessions { get; set; } = null!;

    public DbSet<TradeHistoryChecklist> TradeHistoryChecklist { get; set; } = null!;

    public DbSet<TradeEmotionTag> TradeEmotionTags { get; set; } = null!;

    public DbSet<TechnicalAnalysis> TechnicalAnalyses { get; set; } = null!;

    public DbSet<TradeTechnicalAnalysisTag> TradeTechnicalAnalysisTags { get; set; } = null!;
    
    public DbSet<TradingSummary> TradingSummaries { get; set; } = null!;

    public DbSet<TradingReview> TradingReviews { get; set; } = null!;

    public DbSet<TradingProfile> TradingProfiles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TradingSummary>(builder =>
        {
            builder.ToTable("TradingSummaries", "Trades");
            builder.OwnsOne(ta => ta.CriticalMistakes, cm =>
            {
                cm.ToJson("CriticalMistakes"); 
            });
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
