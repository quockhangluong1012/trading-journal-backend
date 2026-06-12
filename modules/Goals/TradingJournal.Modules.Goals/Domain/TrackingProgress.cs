namespace TradingJournal.Modules.Goals.Domain;

public static class TrackingProgress
{
    public static decimal Calculate(
        TrackingMode mode,
        MetricDirection? direction,
        decimal? startValue,
        decimal? currentValue,
        decimal? targetValue,
        bool isCompleted)
    {
        if (mode == TrackingMode.Manual)
        {
            return isCompleted ? 100m : 0m;
        }

        if (direction is null || startValue is null || currentValue is null || targetValue is null)
        {
            return 0m;
        }

        decimal distance = direction == MetricDirection.AtLeast
            ? targetValue.Value - startValue.Value
            : startValue.Value - targetValue.Value;

        if (distance <= 0m)
        {
            return 0m;
        }

        decimal travelled = direction == MetricDirection.AtLeast
            ? currentValue.Value - startValue.Value
            : startValue.Value - currentValue.Value;

        return Math.Clamp(decimal.Round(travelled / distance * 100m, 2), 0m, 100m);
    }

    public static bool IsMetricComplete(
        MetricDirection direction,
        decimal currentValue,
        decimal targetValue)
    {
        return direction == MetricDirection.AtLeast
            ? currentValue >= targetValue
            : currentValue <= targetValue;
    }
}
