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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // base applies the global !IsDisabled soft-delete filter and audit configuration.
        base.OnModelCreating(modelBuilder);

        // Psychology data is scoped to the owning user; index (CreatedBy, <date>) to avoid
        // per-user full table scans on the user-owned tables.
        modelBuilder.Entity<PsychologyJournal>().HasIndex(j => new { j.CreatedBy, j.Date });
        modelBuilder.Entity<DailyNote>().HasIndex(n => new { n.CreatedBy, n.NoteDate });
        modelBuilder.Entity<TiltSnapshot>().HasIndex(t => new { t.CreatedBy, t.RecordedAt });
        modelBuilder.Entity<StreakRecord>().HasIndex(s => new { s.CreatedBy, s.RecordedAt });
        modelBuilder.Entity<KarmaRecord>().HasIndex(k => new { k.CreatedBy, k.RecordedAt });
        modelBuilder.Entity<KarmaRecord>()
            .HasIndex(k => new { k.CreatedBy, k.ActionType, k.ReferenceId })
            .IsUnique()
            .HasFilter("[ReferenceId] IS NOT NULL");
        modelBuilder.Entity<Achievement>().HasIndex(a => new { a.CreatedBy, a.AchievementType }).IsUnique();
    }
}
