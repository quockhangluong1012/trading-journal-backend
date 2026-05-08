namespace TradingJournal.Modules.Psychology.Infrastructure.Persistance;

public interface IPsychologyDbContext
{
    public DbSet<EmotionTag> EmotionTags { get; set; }

    public DbSet<PsychologyJournal> PsychologyJournals { get; set; }

    public DbSet<PsychologyJournalEmotion> PsychologyJournalEmotions { get; set; }

    public DbSet<TiltSnapshot> TiltSnapshots { get; set; }

    public DbSet<StreakRecord> StreakRecords { get; set; }

    public DbSet<KarmaRecord> KarmaRecords { get; set; }

    public DbSet<Achievement> Achievements { get; set; }

    public DbSet<DailyNote> DailyNotes { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
