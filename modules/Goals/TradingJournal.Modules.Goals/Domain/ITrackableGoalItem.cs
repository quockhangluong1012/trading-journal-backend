namespace TradingJournal.Modules.Goals.Domain;

public interface ITrackableGoalItem
{
    TrackingMode TrackingMode { get; set; }
    string? MetricName { get; set; }
    string? MetricUnit { get; set; }
    MetricDirection? MetricDirection { get; set; }
    decimal? StartValue { get; set; }
    decimal? CurrentValue { get; set; }
    decimal? TargetValue { get; set; }
    bool IsCompleted { get; set; }
    DateTime? CompletedDate { get; set; }
}
