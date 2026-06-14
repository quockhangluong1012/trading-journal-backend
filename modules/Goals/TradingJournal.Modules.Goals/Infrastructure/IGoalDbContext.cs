namespace TradingJournal.Modules.Goals.Infrastructure;

public interface IGoalDbContext
{
    DbSet<Goal> Goals { get; set; }
    DbSet<GoalMilestone> Milestones { get; set; }
    DbSet<GoalTask> GoalTasks { get; set; }
    DbSet<GoalProgressEntry> ProgressEntries { get; set; }
    DbSet<GoalActivityLink> ActivityLinks { get; set; }

    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
