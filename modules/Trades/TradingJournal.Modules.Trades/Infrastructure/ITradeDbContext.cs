namespace TradingJournal.Modules.Trades.Infrastructure;

public interface ITradeDbContext
{
    DbSet<TradeHistory> TradeHistories { get; set; }

    DbSet<ChecklistModel> ChecklistModels { get; set; }

    DbSet<PretradeChecklist> PretradeChecklists { get; set; }

    DbSet<TradeScreenShot> TradeScreenShots { get; set; }

    DbSet<TradingZone> TradingZones { get; set; }

    DbSet<TradeHistoryChecklist> TradeHistoryChecklist { get; set; }

    DbSet<TradeEmotionTag> TradeEmotionTags { get; set; }

    DbSet<TechnicalAnalysis> TechnicalAnalyses { get; set; }

    DbSet<TradeTechnicalAnalysisTag> TradeTechnicalAnalysisTags { get; set; }


    DbSet<TradingSession> TradingSessions { get; set; }



    DbSet<TradingProfile> TradingProfiles { get; set; }

    DbSet<LessonLearned> LessonsLearned { get; set; }

    DbSet<LessonTradeLink> LessonTradeLinks { get; set; }

    DbSet<DisciplineRule> DisciplineRules { get; set; }

    DbSet<DisciplineLog> DisciplineLogs { get; set; }

    DbSet<TradingReview> TradingReviews { get; set; }

    DbSet<ReviewActionItem> ReviewActionItems { get; set; }

    DbSet<TradeTemplate> TradeTemplates { get; set; }

    Task BeginTransaction();

    Task CommitTransaction();

    Task RollbackTransaction();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
