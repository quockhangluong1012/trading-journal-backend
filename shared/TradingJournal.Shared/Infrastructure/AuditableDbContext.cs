using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using TradingJournal.Shared.Abstractions;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Shared.Infrastructure;

/// <summary>
/// Base DbContext that provides automatic audit field population (CreatedDate, CreatedBy, 
/// UpdatedDate, UpdatedBy) and transaction management. All module DbContexts should
/// inherit from this class to avoid duplicating this logic.
/// </summary>
public abstract class AuditableDbContext(DbContextOptions options, IHttpContextAccessor httpContextAccessor)
    : DbContext(options)
{
    private IDbContextTransaction? _transaction;

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

    /// <summary>
    /// Applies a global query filter on all entities that inherit from EntityBase,
    /// automatically excluding soft-deleted records (IsDisabled = true).
    /// Derived DbContexts should call base.OnModelCreating(modelBuilder) to inherit this behavior.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.IsAssignableTo(typeof(EntityBase<int>)))
            {
                var method = typeof(AuditableDbContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);

                method.Invoke(null, [modelBuilder]);
            }
        }
    }

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : EntityBase<int>
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => !e.IsDisabled);
    }
}
