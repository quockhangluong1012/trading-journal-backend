namespace TradingJournal.Modules.AiInsights.Infrastructure;

public interface IAiInsightsDbContext
{
    DbSet<TradingReview> TradingReviews { get; set; }

    Task BeginTransaction();

    Task CommitTransaction();

    Task RollbackTransaction();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
