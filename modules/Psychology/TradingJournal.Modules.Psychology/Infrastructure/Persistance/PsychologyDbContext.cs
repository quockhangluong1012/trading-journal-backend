using Microsoft.EntityFrameworkCore.ChangeTracking;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Psychology.Infrastructure.Persistance;

internal sealed class PsychologyDbContext(DbContextOptions<PsychologyDbContext> options, IHttpContextAccessor httpContextAccessor)
    : DbContext(options), IPsychologyDbContext
{
    public DbSet<EmotionTag> EmotionTags { get; set; } = null!;

    public DbSet<PsychologyJournal> PsychologyJournals { get; set; } = null!;

    public DbSet<PsychologyJournalEmotion> PsychologyJournalEmotions { get; set; } = null!;

    public DbSet<TiltSnapshot> TiltSnapshots { get; set; } = null!;

    public DbSet<StreakRecord> StreakRecords { get; set; } = null!;

    public DbSet<KarmaRecord> KarmaRecords { get; set; } = null!;

    public DbSet<Achievement> Achievements { get; set; } = null!;

    public DbSet<DailyNote> DailyNotes { get; set; } = null!;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;

        foreach (EntityEntry<EntityBase<int>> entry in ChangeTracker.Entries<EntityBase<int>>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedDate = DateTime.UtcNow;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedDate = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
