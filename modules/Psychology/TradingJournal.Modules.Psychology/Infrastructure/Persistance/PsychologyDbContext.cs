using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Psychology.Infrastructure.Persistance;

internal sealed class PsychologyDbContext(DbContextOptions<PsychologyDbContext> options, IHttpContextAccessor httpContextAccessor)
    : DbContext(options), IPsychologyDbContext
{
    private IDbContextTransaction? _transaction;

    public DbSet<EmotionTag> EmotionTags { get; set; } = null!;

    public DbSet<PsychologyJournal> PsychologyJournals { get; set; } = null!;

    public DbSet<PsychologyJournalEmotion> PsychologyJournalEmotions { get; set; } = null!;

    public async Task BeginTransaction()
    {
        _transaction = await Database.BeginTransactionAsync();
    }

    public async Task CommitTransaction()
    {
        if (_transaction == null) return;
        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransaction()
    {
        if (_transaction == null) return;
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

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
