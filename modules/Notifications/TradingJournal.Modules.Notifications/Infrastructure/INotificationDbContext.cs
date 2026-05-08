namespace TradingJournal.Modules.Notifications.Infrastructure;

public interface INotificationDbContext
{
    DbSet<Notification> Notifications { get; set; }

    [Obsolete("Manual transactions are not retry-safe with execution strategies. Wrap the full unit of work in Database.CreateExecutionStrategy().ExecuteAsync instead.")]
    Task BeginTransaction();

    [Obsolete("Manual transactions are not retry-safe with execution strategies. Wrap the full unit of work in Database.CreateExecutionStrategy().ExecuteAsync instead.")]
    Task CommitTransaction();

    [Obsolete("Manual transactions are not retry-safe with execution strategies. Wrap the full unit of work in Database.CreateExecutionStrategy().ExecuteAsync instead.")]
    Task RollbackTransaction();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
