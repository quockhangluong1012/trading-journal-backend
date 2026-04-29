using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "DisciplineRules", Schema = "Trades")]
public sealed class DisciplineRule : EntityBase<int>
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public LessonCategory Category { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display ordering (lower = higher priority).
    /// </summary>
    public int SortOrder { get; set; }

    public ICollection<DisciplineLog> DisciplineLogs { get; set; } = [];
}
