using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.Trades.Infrastructure;

internal sealed class TradeDbContext(DbContextOptions<TradeDbContext> options, IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), ITradeDbContext
{
    private const int TradePricePrecision = 18;
    private const int TradePriceScale = 5;

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
    


    public DbSet<TradingProfile> TradingProfiles { get; set; } = null!;

    public DbSet<LessonLearned> LessonsLearned { get; set; } = null!;

    public DbSet<LessonTradeLink> LessonTradeLinks { get; set; } = null!;

    public DbSet<DisciplineRule> DisciplineRules { get; set; } = null!;

    public DbSet<DisciplineLog> DisciplineLogs { get; set; } = null!;

    public DbSet<TradingReview> TradingReviews { get; set; } = null!;

    public DbSet<ReviewActionItem> ReviewActionItems { get; set; } = null!;

    public DbSet<TradeTemplate> TradeTemplates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TradeHistory>(trade =>
        {
            trade.Property(t => t.EntryPrice).HasPrecision(TradePricePrecision, TradePriceScale);
            trade.Property(t => t.ExitPrice).HasPrecision(TradePricePrecision, TradePriceScale);
            trade.Property(t => t.StopLoss).HasPrecision(TradePricePrecision, TradePriceScale);
            trade.Property(t => t.TargetTier1).HasPrecision(TradePricePrecision, TradePriceScale);
            trade.Property(t => t.TargetTier2).HasPrecision(TradePricePrecision, TradePriceScale);
            trade.Property(t => t.TargetTier3).HasPrecision(TradePricePrecision, TradePriceScale);
        });

        modelBuilder.Entity<TradeTemplate>(template =>
        {
            template.Property(t => t.DefaultStopLoss).HasPrecision(TradePricePrecision, TradePriceScale);
            template.Property(t => t.DefaultTargetTier1).HasPrecision(TradePricePrecision, TradePriceScale);
            template.Property(t => t.DefaultTargetTier2).HasPrecision(TradePricePrecision, TradePriceScale);
            template.Property(t => t.DefaultTargetTier3).HasPrecision(TradePricePrecision, TradePriceScale);
        });
    }
}
