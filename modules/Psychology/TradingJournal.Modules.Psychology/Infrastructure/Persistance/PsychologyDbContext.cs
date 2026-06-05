using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.Psychology.Infrastructure.Persistance;

internal sealed class PsychologyDbContext(DbContextOptions<PsychologyDbContext> options, IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), IPsychologyDbContext
{
    public DbSet<EmotionTag> EmotionTags { get; set; } = null!;

    public DbSet<PsychologyJournal> PsychologyJournals { get; set; } = null!;

    public DbSet<PsychologyJournalEmotion> PsychologyJournalEmotions { get; set; } = null!;

    public DbSet<TiltSnapshot> TiltSnapshots { get; set; } = null!;

    public DbSet<StreakRecord> StreakRecords { get; set; } = null!;

    public DbSet<KarmaRecord> KarmaRecords { get; set; } = null!;

    public DbSet<Achievement> Achievements { get; set; } = null!;

    public DbSet<DailyNote> DailyNotes { get; set; } = null!;
}
