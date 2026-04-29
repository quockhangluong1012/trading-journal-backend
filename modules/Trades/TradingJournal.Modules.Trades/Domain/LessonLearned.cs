using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "LessonsLearned", Schema = "Trades")]
public sealed class LessonLearned : EntityBase<int>
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public LessonCategory Category { get; set; }

    public LessonSeverity Severity { get; set; }

    public LessonStatus Status { get; set; } = LessonStatus.New;

    [MaxLength(500)]
    public string? KeyTakeaway { get; set; }

    [MaxLength(2000)]
    public string? ActionItems { get; set; }

    /// <summary>
    /// Self-assessed impact score from 1 (minor) to 10 (critical).
    /// </summary>
    public int ImpactScore { get; set; } = 5;

    public ICollection<LessonTradeLink> LessonTradeLinks { get; set; } = [];
}
