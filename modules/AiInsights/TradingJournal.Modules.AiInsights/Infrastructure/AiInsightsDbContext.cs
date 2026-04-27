using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.AiInsights.Infrastructure;

internal sealed class AiInsightsDbContext(DbContextOptions<AiInsightsDbContext> options, IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), IAiInsightsDbContext
{
    public DbSet<TradingSummary> TradingSummaries { get; set; } = null!;

    public DbSet<TradingReview> TradingReviews { get; set; } = null!;

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

        modelBuilder.Entity<TradingReview>(builder =>
        {
            builder.ToTable("TradingReviews", "Trades");
        });
    }
}
