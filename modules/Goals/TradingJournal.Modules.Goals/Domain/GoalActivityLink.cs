using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Goals.Domain;

[Table("GoalActivityLinks", Schema = "Goals")]
public sealed class GoalActivityLink : EntityBase<int>
{
    public int GoalId { get; set; }
    public Goal Goal { get; set; } = null!;
    public GoalItemType ItemType { get; set; }
    public int ItemId { get; set; }
    public GoalMetricSource MetricSource { get; set; }
    public GoalActivitySourceType SourceType { get; set; }
    public int SourceId { get; set; }
    public Guid SourceEventId { get; set; }
    public decimal Delta { get; set; }
    public DateTime RecordedAt { get; set; }
    public bool CompletedItem { get; set; }
}
