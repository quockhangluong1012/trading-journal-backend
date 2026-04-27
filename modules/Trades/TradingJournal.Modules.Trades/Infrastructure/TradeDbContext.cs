using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.Trades.Infrastructure;

internal sealed class TradeDbContext(DbContextOptions<TradeDbContext> options, IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), ITradeDbContext
{
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


}

