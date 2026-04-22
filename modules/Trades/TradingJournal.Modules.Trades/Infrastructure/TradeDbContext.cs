using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.Trades.Infrastructure;

internal sealed class TradeDbContext(DbContextOptions<TradeDbContext> options, IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), ITradeDbContext
{
    public DbSet<TradeHistory> TradeHistories { get; set; } = null!;

    public DbSet<ChecklistModel> ChecklistModels { get; set; } = null!;

    public DbSet<TradingSetup> TradingSetups { get; set; } = null!;

    public DbSet<SetupStep> SetupSteps { get; set; } = null!;

    public DbSet<SetupConnection> SetupConnections { get; set; } = null!;

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

        modelBuilder.Entity<TradingSetup>(builder =>
        {
            builder.ToTable("TradingSetups", "Setups");

            builder.HasMany(setup => setup.Steps)
                .WithOne(step => step.TradingSetup)
                .HasForeignKey(step => step.TradingSetupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(setup => setup.Connections)
                .WithOne(connection => connection.TradingSetup)
                .HasForeignKey(connection => connection.TradingSetupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SetupStep>(builder =>
        {
            builder.ToTable("SetupSteps", "Setups");
        });

        modelBuilder.Entity<SetupConnection>(builder =>
        {
            builder.ToTable("SetupConnections", "Setups");

            builder.HasOne(connection => connection.SourceStep)
                .WithMany()
                .HasForeignKey(connection => connection.SourceStepId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(connection => connection.TargetStep)
                .WithMany()
                .HasForeignKey(connection => connection.TargetStepId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TradingSummary>(builder =>
        {
            builder.ToTable("TradingSummaries", "Trades");
            builder.OwnsOne(ta => ta.CriticalMistakes, cm =>
            {
                cm.ToJson("CriticalMistakes"); 
            });
        });
    }
}

