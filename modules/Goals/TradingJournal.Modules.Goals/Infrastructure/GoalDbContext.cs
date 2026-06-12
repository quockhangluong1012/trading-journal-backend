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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Goal>(builder =>
        {
            ConfigureTrackable(builder);
            builder.Property(goal => goal.Title).HasMaxLength(200);
            builder.Property(goal => goal.Description).HasMaxLength(2000);
            builder.HasIndex(goal => new { goal.CreatedBy, goal.IsCompleted, goal.DueDate });

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
        });

        modelBuilder.Entity<GoalMilestone>(builder =>
        {
            ConfigureTrackable(builder);
            builder.Property(milestone => milestone.Title).HasMaxLength(200);
            builder.Property(milestone => milestone.Description).HasMaxLength(2000);
            builder.HasIndex(milestone => new { milestone.GoalId, milestone.SortOrder });
            builder.HasIndex(milestone => milestone.CreatedBy);

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
    }

    private static void ConfigureTrackable<T>(EntityTypeBuilder<T> builder)
        where T : EntityBase<int>, ITrackableGoalItem
    {
        builder.Property(item => item.MetricName).HasMaxLength(100);
        builder.Property(item => item.MetricUnit).HasMaxLength(50);
        builder.Property(item => item.StartValue).HasPrecision(18, 4);
        builder.Property(item => item.CurrentValue).HasPrecision(18, 4);
        builder.Property(item => item.TargetValue).HasPrecision(18, 4);
    }
}
