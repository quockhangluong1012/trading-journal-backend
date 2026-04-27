namespace TradingJournal.Modules.Notifications.Infrastructure;

public interface INotificationDbContext
{
    DbSet<Notification> Notifications { get; set; }

    Task BeginTransaction();

    Task CommitTransaction();

    Task RollbackTransaction();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
