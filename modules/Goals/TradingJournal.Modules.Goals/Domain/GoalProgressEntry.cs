using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Goals.Domain;

[Table("GoalProgressEntries", Schema = "Goals")]
public sealed class GoalProgressEntry : EntityBase<int>
{
    public int GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public GoalItemType ItemType { get; set; }
    public int? MilestoneId { get; set; }
    public GoalMilestone? Milestone { get; set; }
    public int? GoalTaskId { get; set; }
    public GoalTask? GoalTask { get; set; }
    public decimal? PreviousValue { get; set; }
    public decimal? CurrentValue { get; set; }
    public bool PreviousIsCompleted { get; set; }
    public bool CurrentIsCompleted { get; set; }
    public string? Note { get; set; }
}
