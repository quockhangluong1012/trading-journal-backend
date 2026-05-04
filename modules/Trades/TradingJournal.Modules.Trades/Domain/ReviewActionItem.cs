using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "ReviewActionItems", Schema = "Trades")]
public sealed class ReviewActionItem : EntityBase<int>
{
    public int TradingReviewId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public ActionItemPriority Priority { get; set; } = ActionItemPriority.Medium;

    public ActionItemStatus Status { get; set; } = ActionItemStatus.Open;

    public LessonCategory Category { get; set; }

    public DateTimeOffset? DueDate { get; set; }

    public DateTimeOffset? CompletedDate { get; set; }

    [MaxLength(500)]
    public string? CompletionNotes { get; set; }

    [ForeignKey(nameof(TradingReviewId))]
    public TradingReview TradingReview { get; set; } = null!;
}
