using TradingJournal.Tests.Goals.Helpers;

namespace TradingJournal.Tests.Goals.Features.V1;

public sealed class CreateGoalValidatorTests
{
    private static readonly CreateGoal.Validator Validator = new();

    [Fact]
    public void MetricGoal_RequiresMetricNameAndTarget()
    {
        var request = new CreateGoal.Request(
            "Build consistency",
            null,
            null,
            null,
            new TrackingInput(TrackingMode.Metric, null, "trades", MetricDirection.AtLeast, 0m, null),
            1);

        TestValidationResult<CreateGoal.Request> result = Validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("Tracking.MetricName");
        result.ShouldHaveValidationErrorFor("Tracking.TargetValue");
    }

    [Theory]
    [InlineData(MetricDirection.AtLeast, 10, 10)]
    [InlineData(MetricDirection.AtLeast, 10, 5)]
    [InlineData(MetricDirection.AtMost, 10, 10)]
    [InlineData(MetricDirection.AtMost, 10, 15)]
    public void MetricGoal_TargetMustMoveInConfiguredDirection(
        MetricDirection direction,
        decimal startValue,
        decimal targetValue)
    {
        var request = new CreateGoal.Request(
            "Metric goal",
            null,
            null,
            null,
            new TrackingInput(TrackingMode.Metric, "Win rate", "%", direction, startValue, targetValue),
            1);

        Validator.TestValidate(request).ShouldHaveValidationErrorFor("Tracking.TargetValue");
    }

    [Fact]
    public void DueDateCannotPrecedeStartDate()
    {
        var request = new CreateGoal.Request(
            "Goal",
            null,
            new DateTime(2026, 6, 10),
            new DateTime(2026, 6, 9),
            TrackingInput.Manual,
            1);

        Validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.DueDate);
    }
}

public sealed class CreateGoalHandlerTests
{
    [Fact]
    public async Task Handle_CreatesMetricGoalForCurrentUser()
    {
        var goals = new List<Goal>();
        Mock<DbSet<Goal>> goalsMock = DbSetMockHelper.CreateMockDbSet(goals);
        Goal? captured = null;
        goalsMock.Setup(set => set.Add(It.IsAny<Goal>())).Callback<Goal>(goal => captured = goal);

        var context = new Mock<IGoalDbContext>();
        context.Setup(c => c.Goals).Returns(goalsMock.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateGoal.Handler(context.Object);
        var request = new CreateGoal.Request(
            "  Execute 100 A+ setups  ",
            "  Track only qualified setups  ",
            null,
            new DateTime(2026, 12, 31),
            new TrackingInput(TrackingMode.Metric, "Qualified setups", "trades", MetricDirection.AtLeast, 0m, 100m),
            42);

        Result<int> result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(42, captured!.CreatedBy);
        Assert.Equal("Execute 100 A+ setups", captured.Title);
        Assert.Equal(0m, captured.CurrentValue);
        Assert.False(captured.IsCompleted);
    }
}

public sealed class AddMilestoneHandlerTests
{
    [Fact]
    public async Task Handle_RejectsGoalOwnedByAnotherUser()
    {
        var goals = new List<Goal> { new() { Id = 7, CreatedBy = 99, Title = "Private goal" } };
        var context = new Mock<IGoalDbContext>();
        context.Setup(c => c.Goals).Returns(DbSetMockHelper.CreateMockDbSet(goals).Object);

        var handler = new AddMilestone.Handler(context.Object);
        var request = new AddMilestone.Request(7, "First milestone", null, null, 0, TrackingInput.Manual, 42);

        Result<int> result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

public sealed class AddGoalTaskHandlerTests
{
    [Fact]
    public async Task Handle_RejectsMilestoneFromDifferentGoal()
    {
        var goals = new List<Goal> { new() { Id = 7, CreatedBy = 42, Title = "My goal" } };
        var milestones = new List<GoalMilestone>
        {
            new() { Id = 3, GoalId = 8, CreatedBy = 42, Title = "Other goal milestone" },
        };
        var context = new Mock<IGoalDbContext>();
        context.Setup(c => c.Goals).Returns(DbSetMockHelper.CreateMockDbSet(goals).Object);
        context.Setup(c => c.Milestones).Returns(DbSetMockHelper.CreateMockDbSet(milestones).Object);

        var handler = new AddGoalTask.Handler(context.Object);
        var request = new AddGoalTask.Request(7, 3, "Review journal", null, null, 0, TrackingInput.Manual, 42);

        Result<int> result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

public sealed class UpdateProgressHandlerTests
{
    [Fact]
    public async Task MetricUpdate_SetsValueCompletesItemAndWritesHistory()
    {
        var goal = new Goal
        {
            Id = 7,
            CreatedBy = 42,
            Title = "Execute setups",
            TrackingMode = TrackingMode.Metric,
            MetricName = "Qualified setups",
            MetricDirection = MetricDirection.AtLeast,
            StartValue = 0m,
            CurrentValue = 80m,
            TargetValue = 100m,
        };
        Mock<DbSet<GoalProgressEntry>> entriesMock = DbSetMockHelper.CreateMockDbSet(Array.Empty<GoalProgressEntry>());
        GoalProgressEntry? capturedEntry = null;
        entriesMock.Setup(set => set.Add(It.IsAny<GoalProgressEntry>()))
            .Callback<GoalProgressEntry>(entry => capturedEntry = entry);

        var context = new Mock<IGoalDbContext>();
        context.Setup(c => c.Goals).Returns(DbSetMockHelper.CreateMockDbSet(new[] { goal }).Object);
        context.Setup(c => c.ProgressEntries).Returns(entriesMock.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProgress.GoalHandler(context.Object);
        var request = new UpdateProgress.GoalRequest(7, 100m, null, "Reached target", 42);

        Result<ProgressResult> result = await handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, goal.CurrentValue);
        Assert.True(goal.IsCompleted);
        Assert.NotNull(goal.CompletedDate);
        Assert.Equal(100m, result.Value.ProgressPercent);
        Assert.Equal(80m, capturedEntry!.PreviousValue);
        Assert.Equal(100m, capturedEntry.CurrentValue);
    }

    [Fact]
    public async Task ManualUpdate_UsesCompletionFlagAndRejectsMetricValue()
    {
        var goal = new Goal
        {
            Id = 7,
            CreatedBy = 42,
            Title = "Manual goal",
            TrackingMode = TrackingMode.Manual,
        };
        var context = new Mock<IGoalDbContext>();
        context.Setup(c => c.Goals).Returns(DbSetMockHelper.CreateMockDbSet(new[] { goal }).Object);
        context.Setup(c => c.ProgressEntries)
            .Returns(DbSetMockHelper.CreateMockDbSet(Array.Empty<GoalProgressEntry>()).Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new UpdateProgress.GoalHandler(context.Object);

        Result<ProgressResult> invalid = await handler.Handle(
            new UpdateProgress.GoalRequest(7, 1m, null, null, 42),
            CancellationToken.None);
        Result<ProgressResult> valid = await handler.Handle(
            new UpdateProgress.GoalRequest(7, null, true, "Done", 42),
            CancellationToken.None);

        Assert.True(invalid.IsFailure);
        Assert.True(valid.IsSuccess);
        Assert.True(goal.IsCompleted);
        Assert.Equal(100m, valid.Value.ProgressPercent);
    }
}
