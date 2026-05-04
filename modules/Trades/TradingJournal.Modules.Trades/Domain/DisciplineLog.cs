using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "DisciplineLogs", Schema = "Trades")]
public sealed class DisciplineLog : EntityBase<int>
{
    public int DisciplineRuleId { get; set; }

    /// <summary>
    /// Optional link to a specific trade. Null for general daily checks.
    /// </summary>
    public int? TradeHistoryId { get; set; }

    public bool WasFollowed { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTimeOffset Date { get; set; }

    [ForeignKey(nameof(DisciplineRuleId))]
    public DisciplineRule DisciplineRule { get; set; } = null!;

    [ForeignKey(nameof(TradeHistoryId))]
    public TradeHistory? TradeHistory { get; set; }
}
