using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Contracts;
using TradingJournal.Modules.Goals.Common.Enum;
using TradingJournal.Modules.Goals.Domain;
using TradingJournal.Modules.Goals.Services;
using TradingJournal.Modules.Goals.Infrastructure;

namespace TradingJournal.Tests.Goals;

public sealed class GoalActivityProgressTests
{
    [Fact]
    public async Task ApplyAsync_UpdatesEveryMatchingItemAndPublishesCompletionEvents()
    {
        var goal = new Goal
        {
            Id = 1,
            CreatedBy = 42,
            Title = "Complete profitable trades",
            TrackingMode = TrackingMode.Metric,
            MetricSource = GoalMetricSource.WinningTradeCount,
            MetricDirection = MetricDirection.AtLeast,
            StartValue = 0m,
            CurrentValue = 4m,
            TargetValue = 5m,
        };
        var milestone = new GoalMilestone
        {
            Id = 2,
            GoalId = 1,
            CreatedBy = 42,
            Title = "First five wins",
            TrackingMode = TrackingMode.Metric,
            MetricSource = GoalMetricSource.WinningTradeCount,
            MetricDirection = MetricDirection.AtLeast,
            StartValue = 0m,
            CurrentValue = 4m,
            TargetValue = 5m,
        };
        var task = new GoalTask
        {
            Id = 3,
            GoalId = 1,
            MilestoneId = 2,
            CreatedBy = 42,
            Title = "Record the fifth win",
            TrackingMode = TrackingMode.Metric,
            MetricSource = GoalMetricSource.WinningTradeCount,
            MetricDirection = MetricDirection.AtLeast,
            StartValue = 0m,
            CurrentValue = 4m,
            TargetValue = 5m,
        };
        await using GoalDbContext context = CreateContext();
        context.AddRange(goal, milestone, task);
        await context.SaveChangesAsync();
        var eventBus = new Mock<IEventBus>();
        var service = new GoalActivityService(context, eventBus.Object);
        Guid eventId = Guid.NewGuid();

        await service.ApplyAsync(
            eventId,
            42,
            GoalMetricSource.WinningTradeCount,
            1m,
            GoalActivitySourceType.TradeHistory,
            99,
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.Equal(5m, goal.CurrentValue);
        Assert.True(goal.IsCompleted);
        Assert.True(milestone.IsCompleted);
        Assert.True(task.IsCompleted);
        Assert.Equal(3, await context.ActivityLinks.CountAsync());
        Assert.Equal(3, await context.ProgressEntries.CountAsync());
        Assert.All(await context.ActivityLinks.ToListAsync(), link => Assert.True(link.CompletedItem));
        eventBus.Verify(bus => bus.PublishAsync(
            It.IsAny<GoalItemCompletedEvent>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ApplyAsync_DoesNotApplyTheSameEventTwice()
    {
        var goal = new Goal
        {
            Id = 1,
            CreatedBy = 42,
            Title = "Complete backtests",
            TrackingMode = TrackingMode.Metric,
            MetricSource = GoalMetricSource.BacktestSessionCount,
            MetricDirection = MetricDirection.AtLeast,
            StartValue = 0m,
            CurrentValue = 0m,
            TargetValue = 10m,
        };
        await using GoalDbContext context = CreateContext();
        context.Goals.Add(goal);
        await context.SaveChangesAsync();
        var service = new GoalActivityService(context, Mock.Of<IEventBus>());
        Guid eventId = Guid.NewGuid();

        await service.ApplyAsync(eventId, 42, GoalMetricSource.BacktestSessionCount, 1m,
            GoalActivitySourceType.BacktestSession, 7, DateTime.UtcNow, CancellationToken.None);
        await service.ApplyAsync(eventId, 42, GoalMetricSource.BacktestSessionCount, 1m,
            GoalActivitySourceType.BacktestSession, 7, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1m, goal.CurrentValue);
        Assert.Equal(1, await context.ActivityLinks.CountAsync());
        Assert.Equal(1, await context.ProgressEntries.CountAsync());
    }

    private static GoalDbContext CreateContext()
    {
        DbContextOptions<GoalDbContext> options = new DbContextOptionsBuilder<GoalDbContext>()
            .UseInMemoryDatabase($"goals-{Guid.NewGuid()}")
            .Options;
        return new GoalDbContext(options, new HttpContextAccessor());
    }
}
