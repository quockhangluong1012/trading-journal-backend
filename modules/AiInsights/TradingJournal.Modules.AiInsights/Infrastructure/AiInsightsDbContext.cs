using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.AiInsights.Infrastructure;

internal sealed class AiInsightsDbContext(DbContextOptions<AiInsightsDbContext> options, IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), IAiInsightsDbContext
{
    public DbSet<AiCoachConversation> AiCoachConversations { get; set; } = null!;

    public DbSet<MorningBriefing> MorningBriefings { get; set; } = null!;

    public DbSet<TradingReview> TradingReviews { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AiCoachConversation>(builder =>
        {
            builder.ToTable("AiCoachConversations", "Trades");
            builder.HasIndex(entity => new { entity.CreatedBy, entity.CreatedDate });
            builder.Property(entity => entity.Mode).HasMaxLength(32);
        });

        modelBuilder.Entity<MorningBriefing>(builder =>
        {
            builder.ToTable("MorningBriefings", "Trades");
            builder.HasIndex(entity => new { entity.CreatedBy, entity.BriefingDateUtc }).IsUnique();
            builder.Property(entity => entity.Greeting).HasMaxLength(256);
            builder.Property(entity => entity.ActionItem).HasMaxLength(512);
            builder.Property(entity => entity.OverallMood).HasMaxLength(32);
        });

        modelBuilder.Entity<TradingReview>(builder =>
        {
            builder.ToTable("TradingReviews", "Trades", tableBuilder => tableBuilder.ExcludeFromMigrations());
        });
    }
}
