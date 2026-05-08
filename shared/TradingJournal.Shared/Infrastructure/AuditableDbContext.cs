using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using TradingJournal.Shared.Abstractions;
using TradingJournal.Shared.Audit;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Shared.Infrastructure;

/// <summary>
/// Base DbContext that provides automatic audit field population (CreatedDate, CreatedBy, 
/// UpdatedDate, UpdatedBy), transaction management, and change-tracking audit trail.
/// All module DbContexts should inherit from this class to avoid duplicating this logic.
/// </summary>
public abstract class AuditableDbContext(DbContextOptions options, IHttpContextAccessor httpContextAccessor)
    : DbContext(options)
{
    private IDbContextTransaction? _transaction;

    [Obsolete("Manual transactions are not retry-safe with execution strategies. Use ExecuteInTransactionAsync or Database.CreateExecutionStrategy().ExecuteAsync instead.")]
    public async Task BeginTransaction()
    {
        _transaction = await Database.BeginTransactionAsync();
    }

    [Obsolete("Manual transactions are not retry-safe with execution strategies. Use ExecuteInTransactionAsync or Database.CreateExecutionStrategy().ExecuteAsync instead.")]
    public async Task CommitTransaction()
    {
        if (_transaction == null) return;
        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    [Obsolete("Manual transactions are not retry-safe with execution strategies. Use ExecuteInTransactionAsync or Database.CreateExecutionStrategy().ExecuteAsync instead.")]
    public async Task RollbackTransaction()
    {
        if (_transaction == null) return;
        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction = await Database.BeginTransactionAsync(cancellationToken);

            try
            {
                TResult result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        int userId = httpContextAccessor.HttpContext?.User.GetCurrentUserId() ?? 0;

        // Capture audit entries BEFORE SaveChanges modifies the state
        List<AuditEntryBuilder> auditBuilders = BuildAuditEntries(userId);

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

        int result = await base.SaveChangesAsync(cancellationToken);

        // Persist audit entries after save so generated keys are available.
        if (auditBuilders.Count > 0)
        {
            await PersistAuditEntriesAsync(auditBuilders, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Builds audit entry records from the current ChangeTracker state.
    /// Must be called BEFORE base.SaveChangesAsync() since Added entities
    /// won't have their IDs until after the save.
    /// </summary>
    private List<AuditEntryBuilder> BuildAuditEntries(int userId)
    {
        var builders = new List<AuditEntryBuilder>();

        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            // Skip entries that aren't tracked entity types
            if (entry.Entity is AuditEntry ||
                entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            string entityName = entry.Entity.GetType().Name;
            string entityId = GetPrimaryKeyValue(entry);

            var builder = new AuditEntryBuilder
            {
                EntityName = entityName,
                EntityId = entityId,
                TrackedEntry = entry,
                ChangedBy = userId,
                Action = entry.State switch
                {
                    EntityState.Added => "Create",
                    EntityState.Modified => "Update",
                    EntityState.Deleted => "Delete",
                    _ => "Unknown"
                }
            };

            foreach (PropertyEntry property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;

                // Skip shadow properties and non-relational properties
                if (property.Metadata.IsPrimaryKey())
                    continue;

                switch (entry.State)
                {
                    case EntityState.Added:
                        builder.NewValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        builder.OldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                        {
                            builder.OldValues[propertyName] = property.OriginalValue;
                            builder.NewValues[propertyName] = property.CurrentValue;
                            builder.AffectedColumns.Add(propertyName);
                        }
                        break;
                }
            }

            // Only add if there are actual changes to record
            if (builder.OldValues.Count > 0 || builder.NewValues.Count > 0)
            {
                builders.Add(builder);
            }
        }

        return builders;
    }

    private static string GetPrimaryKeyValue(EntityEntry entry)
    {
        var keyValues = entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString() ?? "0")
            .ToList();

        return string.Join(",", keyValues);
    }

    private async Task PersistAuditEntriesAsync(List<AuditEntryBuilder> builders, CancellationToken ct)
    {
        IAuditLogStore? store = httpContextAccessor.HttpContext?.RequestServices
            .GetService<IAuditLogStore>();

        if (store is null) return;

        List<AuditEntry> entries = builders.Select(b => b.ToAuditEntry()).ToList();
        await store.SaveAsync(entries, ct);
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
