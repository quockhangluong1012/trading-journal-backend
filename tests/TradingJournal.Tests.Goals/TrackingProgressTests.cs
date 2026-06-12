namespace TradingJournal.Tests.Goals;

public sealed class TrackingProgressTests
{
    [Theory]
    [InlineData(10, 60, 110, 50)]
    [InlineData(10, 5, 110, 0)]
    [InlineData(10, 150, 110, 100)]
    public void Calculate_AtLeast_ReturnsClampedPercentage(
        decimal startValue,
        decimal currentValue,
        decimal targetValue,
        decimal expected)
    {
        decimal progress = TrackingProgress.Calculate(
            TrackingMode.Metric,
            MetricDirection.AtLeast,
            startValue,
            currentValue,
            targetValue,
            isCompleted: false);

        Assert.Equal(expected, progress);
    }

    [Theory]
    [InlineData(10, 7, 4, 50)]
    [InlineData(10, 12, 4, 0)]
    [InlineData(10, 2, 4, 100)]
    public void Calculate_AtMost_ReturnsClampedPercentage(
        decimal startValue,
        decimal currentValue,
        decimal targetValue,
        decimal expected)
    {
        decimal progress = TrackingProgress.Calculate(
            TrackingMode.Metric,
            MetricDirection.AtMost,
            startValue,
            currentValue,
            targetValue,
            isCompleted: false);

        Assert.Equal(expected, progress);
    }

    [Theory]
    [InlineData(MetricDirection.AtLeast, 100, 100, true)]
    [InlineData(MetricDirection.AtLeast, 99, 100, false)]
    [InlineData(MetricDirection.AtMost, 5, 5, true)]
    [InlineData(MetricDirection.AtMost, 6, 5, false)]
    public void IsMetricComplete_UsesDirection(
        MetricDirection direction,
        decimal currentValue,
        decimal targetValue,
        bool expected)
    {
        Assert.Equal(expected, TrackingProgress.IsMetricComplete(direction, currentValue, targetValue));
    }

    [Fact]
    public void Calculate_Manual_UsesCompletionFlag()
    {
        Assert.Equal(0m, TrackingProgress.Calculate(TrackingMode.Manual, null, null, null, null, false));
        Assert.Equal(100m, TrackingProgress.Calculate(TrackingMode.Manual, null, null, null, null, true));
    }
}
