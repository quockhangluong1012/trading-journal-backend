using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Goals.Domain;

[Table("GoalTasks", Schema = "Goals")]
public sealed class GoalTask : EntityBase<int>, ITrackableGoalItem
{
    public int GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public int? MilestoneId { get; set; }
    public GoalMilestone? Milestone { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public int SortOrder { get; set; }
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
    public ICollection<GoalProgressEntry> ProgressEntries { get; set; } = [];
}
