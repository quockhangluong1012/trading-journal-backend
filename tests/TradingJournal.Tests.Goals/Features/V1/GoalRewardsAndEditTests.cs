using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Goals.Infrastructure;
using TradingJournal.Tests.Goals.Helpers;

namespace TradingJournal.Tests.Goals.Features.V1;

public sealed class GetGoalStatsTests
{
    [Fact]
    public void Build_CountsCompletionAndAveragesRollup()
    {
        var active = new Goal
        {
            Id = 1,
            TrackingMode = TrackingMode.Manual,
            IsCompleted = false,
            Tasks =
            [
                new GoalTask { Id = 1, TrackingMode = TrackingMode.Manual, IsCompleted = true },
                new GoalTask { Id = 2, TrackingMode = TrackingMode.Manual, IsCompleted = false },
            ],
        };
        var done = new Goal { Id = 2, TrackingMode = TrackingMode.Manual, IsCompleted = true };

        GoalStats stats = GetGoalStats.Build([active, done]);

        Assert.Equal(1, stats.ActiveCount);
        Assert.Equal(1, stats.CompletedCount);
        // active rollup 50 (1/2 tasks) + done 100 → average 75
        Assert.Equal(75m, stats.AverageProgressPercent);
    }

    [Fact]
    public void Build_EmptyReturnsZeroes()
    {
        GoalStats stats = GetGoalStats.Build([]);
        Assert.Equal(0, stats.ActiveCount);
        Assert.Equal(0, stats.CompletedCount);
        Assert.Equal(0m, stats.AverageProgressPercent);
    }
}

public sealed class FirstCompletionGuardTests
{
    [Fact]
    public async Task ManualCompletion_PublishesOnlyOnFirstEverCompletion()
    {
        var goal = new Goal
        {
            Id = 7,
            CreatedBy = 42,
            Title = "Manual goal",
            TrackingMode = TrackingMode.Manual,
        };

        var eventBus = new Mock<IEventBus>();
        var context = BuildContext(goal);
        var handler = new UpdateProgress.GoalHandler(context.Object, eventBus.Object);

        // First completion → publishes and records FirstCompletedDate.
        Result<ProgressResult> first = await handler.Handle(
            new UpdateProgress.GoalRequest(7, null, true, null, 42), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.NotNull(goal.FirstCompletedDate);

        // Re-open then re-complete → no second reward.
        await handler.Handle(new UpdateProgress.GoalRequest(7, null, false, null, 42), CancellationToken.None);
        await handler.Handle(new UpdateProgress.GoalRequest(7, null, true, null, 42), CancellationToken.None);

        eventBus.Verify(
            bus => bus.PublishAsync(It.IsAny<GoalItemCompletedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IGoalDbContext> BuildContext(Goal goal)
    {
        var context = new Mock<IGoalDbContext>();
        context.Setup(c => c.Goals).Returns(DbSetMockHelper.CreateMockDbSet(new[] { goal }).Object);
        context.Setup(c => c.ProgressEntries)
            .Returns(DbSetMockHelper.CreateMockDbSet(Array.Empty<GoalProgressEntry>()).Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        context.SetupTransactionPassthrough<Result<ProgressResult>>();
        return context;
    }
}

public sealed class GoalEditDeleteHandlerTests
{
    [Fact]
    public async Task UpdateGoal_EditsMetadataWithoutWipingMetricProgress()
    {
        var goal = new Goal
        {
            Id = 7,
            CreatedBy = 42,
            Title = "Old",
            TrackingMode = TrackingMode.Metric,
            MetricName = "Trades",
            MetricDirection = MetricDirection.AtLeast,
            StartValue = 0m,
            CurrentValue = 40m,
            TargetValue = 100m,
        };
        await using GoalDbContext context = CreateContext();
        context.Goals.Add(goal);
        await context.SaveChangesAsync();

        var handler = new UpdateGoal.Handler(context);
        var request = new UpdateGoal.Request(
            "New title",
            "New description",
            null,
            null,
            new TrackingInput(TrackingMode.Metric, "Trades", "trades", MetricDirection.AtLeast, 0m, 150m),
            GoalId: 7,
            UserId: 42);

        Result result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New title", goal.Title);
        Assert.Equal(150m, goal.TargetValue);
        Assert.Equal(40m, goal.CurrentValue); // progress preserved across target bump
        Assert.False(goal.IsCompleted);
    }

    [Fact]
    public async Task DeleteGoal_SoftDeletesEntireSubtree()
    {
        var goal = new Goal { Id = 7, CreatedBy = 42, Title = "Doomed" };
        var milestone = new GoalMilestone { Id = 3, GoalId = 7, CreatedBy = 42, Title = "M" };
        var task = new GoalTask { Id = 5, GoalId = 7, MilestoneId = 3, CreatedBy = 42, Title = "T" };
        await using GoalDbContext context = CreateContext();
        context.AddRange(goal, milestone, task);
        await context.SaveChangesAsync();

        var handler = new DeleteGoal.Handler(context);
        Result result = await handler.Handle(new DeleteGoal.Request(7, 42), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Query filter now hides the whole subtree.
        Assert.Equal(0, await context.Goals.CountAsync());
        Assert.Equal(0, await context.Milestones.CountAsync());
        Assert.Equal(0, await context.GoalTasks.CountAsync());
        // The rows still exist (soft delete), just hidden by the query filter.
        Assert.True(await context.GoalTasks.IgnoreQueryFilters().AllAsync(t => t.IsDisabled));
        Assert.True(await context.Milestones.IgnoreQueryFilters().AllAsync(m => m.IsDisabled));
    }

    [Fact]
    public async Task ReorderItems_AppliesNewSortOrders()
    {
        var goal = new Goal { Id = 7, CreatedBy = 42, Title = "G" };
        var m1 = new GoalMilestone { Id = 1, GoalId = 7, CreatedBy = 42, Title = "M1", SortOrder = 0 };
        var m2 = new GoalMilestone { Id = 2, GoalId = 7, CreatedBy = 42, Title = "M2", SortOrder = 1 };
        await using GoalDbContext context = CreateContext();
        context.AddRange(goal, m1, m2);
        await context.SaveChangesAsync();

        var handler = new ReorderItems.Handler(context);
        var request = new ReorderItems.Request(
            [
                new ReorderItems.ReorderEntry(GoalItemType.Milestone, 1, 5),
                new ReorderItems.ReorderEntry(GoalItemType.Milestone, 2, 0),
            ],
            GoalId: 7,
            UserId: 42);

        Result result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, m1.SortOrder);
        Assert.Equal(0, m2.SortOrder);
    }

    private static GoalDbContext CreateContext()
    {
        DbContextOptions<GoalDbContext> options = new DbContextOptionsBuilder<GoalDbContext>()
            .UseInMemoryDatabase($"goals-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new GoalDbContext(options, new HttpContextAccessor());
    }
}
