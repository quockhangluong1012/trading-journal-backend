using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Goals.Domain;

[Table("Goals", Schema = "Goals")]
public sealed class Goal : EntityBase<int>, ITrackableGoalItem
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public TrackingMode TrackingMode { get; set; } = TrackingMode.Manual;
    public GoalMetricSource? MetricSource { get; set; }
    public string? MetricName { get; set; }
    public string? MetricUnit { get; set; }
    public MetricDirection? MetricDirection { get; set; }
    public decimal? StartValue { get; set; }
    public decimal? CurrentValue { get; set; }
    public decimal? TargetValue { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? FirstCompletedDate { get; set; }
    public ICollection<GoalMilestone> Milestones { get; set; } = [];
    public ICollection<GoalTask> Tasks { get; set; } = [];
    public ICollection<GoalProgressEntry> ProgressEntries { get; set; } = [];
    public ICollection<GoalActivityLink> ActivityLinks { get; set; } = [];
}
