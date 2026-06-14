using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Modules.Goals.Infrastructure;

internal sealed class GoalDbContext(
    DbContextOptions<GoalDbContext> options,
    IHttpContextAccessor httpContextAccessor)
    : AuditableDbContext(options, httpContextAccessor), IGoalDbContext
{
    public DbSet<Goal> Goals { get; set; } = null!;
    public DbSet<GoalMilestone> Milestones { get; set; } = null!;
    public DbSet<GoalTask> GoalTasks { get; set; } = null!;
    public DbSet<GoalProgressEntry> ProgressEntries { get; set; } = null!;
    public DbSet<GoalActivityLink> ActivityLinks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Goal>(builder =>
        {
            ConfigureTrackable(builder);
            builder.Property(goal => goal.Title).HasMaxLength(200);
            builder.Property(goal => goal.Description).HasMaxLength(2000);
            builder.HasIndex(goal => new { goal.CreatedBy, goal.IsCompleted, goal.DueDate });
            ConfigureAutoTrackingIndex(builder);

            builder.HasMany(goal => goal.Milestones)
                .WithOne(milestone => milestone.Goal)
                .HasForeignKey(milestone => milestone.GoalId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(goal => goal.Tasks)
                .WithOne(task => task.Goal)
                .HasForeignKey(task => task.GoalId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(goal => goal.ProgressEntries)
                .WithOne(entry => entry.Goal)
                .HasForeignKey(entry => entry.GoalId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(goal => goal.ActivityLinks)
                .WithOne(link => link.Goal)
                .HasForeignKey(link => link.GoalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoalMilestone>(builder =>
        {
            ConfigureTrackable(builder);
            builder.Property(milestone => milestone.Title).HasMaxLength(200);
            builder.Property(milestone => milestone.Description).HasMaxLength(2000);
            builder.HasIndex(milestone => new { milestone.GoalId, milestone.SortOrder });
            builder.HasIndex(milestone => milestone.CreatedBy);
            ConfigureAutoTrackingIndex(builder);

            builder.HasMany(milestone => milestone.Tasks)
                .WithOne(task => task.Milestone)
                .HasForeignKey(task => task.MilestoneId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasMany(milestone => milestone.ProgressEntries)
                .WithOne(entry => entry.Milestone)
                .HasForeignKey(entry => entry.MilestoneId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<GoalTask>(builder =>
        {
            ConfigureTrackable(builder);
            builder.Property(task => task.Title).HasMaxLength(200);
            builder.Property(task => task.Description).HasMaxLength(2000);
            builder.HasIndex(task => new { task.GoalId, task.MilestoneId, task.SortOrder });
            builder.HasIndex(task => task.CreatedBy);
            ConfigureAutoTrackingIndex(builder);

            builder.HasMany(task => task.ProgressEntries)
                .WithOne(entry => entry.GoalTask)
                .HasForeignKey(entry => entry.GoalTaskId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<GoalProgressEntry>(builder =>
        {
            builder.Property(entry => entry.PreviousValue).HasPrecision(18, 4);
            builder.Property(entry => entry.CurrentValue).HasPrecision(18, 4);
            builder.Property(entry => entry.Note).HasMaxLength(1000);
            builder.HasIndex(entry => new { entry.GoalId, entry.CreatedDate });
            builder.HasIndex(entry => entry.MilestoneId);
            builder.HasIndex(entry => entry.GoalTaskId);
        });

        modelBuilder.Entity<GoalActivityLink>(builder =>
        {
            builder.Property(link => link.Delta).HasPrecision(18, 4);
            builder.HasIndex(link => new { link.SourceEventId, link.ItemType, link.ItemId }).IsUnique();
            builder.HasIndex(link => new { link.GoalId, link.RecordedAt });
            builder.HasIndex(link => new { link.SourceType, link.SourceId });
        });
    }

    // Auto-tracking (GoalActivityService.ApplyAsync) fans out one query per trackable
    // table on every trade/backtest event, filtering by exactly these four columns. A
    // filtered index over only the still-open items keeps that hot path off table scans.
    private static void ConfigureAutoTrackingIndex<T>(EntityTypeBuilder<T> builder)
        where T : EntityBase<int>, ITrackableGoalItem =>
        builder
            .HasIndex(item => new { item.CreatedBy, item.MetricSource, item.TrackingMode, item.IsCompleted })
            .HasFilter("[IsCompleted] = 0");

    private static void ConfigureTrackable<T>(EntityTypeBuilder<T> builder)
        where T : EntityBase<int>, ITrackableGoalItem
    {
        // Soft-deleted goals/milestones/tasks are hidden from every read (including
        // Include() navigations) so DeleteX handlers only need to flip IsDisabled.
        builder.HasQueryFilter(item => !item.IsDisabled);
        builder.Property(item => item.MetricName).HasMaxLength(100);
        builder.Property(item => item.MetricUnit).HasMaxLength(50);
        builder.Property(item => item.StartValue).HasPrecision(18, 4);
        builder.Property(item => item.CurrentValue).HasPrecision(18, 4);
        builder.Property(item => item.TargetValue).HasPrecision(18, 4);
    }
}
