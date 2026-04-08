namespace TradingJournal.Modules.Psychology.Infrastructure.Persistance;

public interface IPsychologyDbContext
{
    public DbSet<EmotionTag> EmotionTags { get; set; }

    public DbSet<PsychologyJournal> PsychologyJournals { get; set; }

    public DbSet<PsychologyJournalEmotion> PsychologyJournalEmotions { get; set; }

    Task BeginTransaction();

    Task CommitTransaction();

    Task RollbackTransaction();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
