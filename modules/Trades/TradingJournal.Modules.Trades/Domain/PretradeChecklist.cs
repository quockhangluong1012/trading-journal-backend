using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "PretradeChecklists", Schema = "Trades")]
public sealed class PretradeChecklist : EntityBase<int>
{
    public string Name { get; set; } = string.Empty;

    public PretradeChecklistType CheckListType { get; set; }

    public int ChecklistModelId { get; set; }

    [ForeignKey(nameof(ChecklistModelId))]
    public ChecklistModel ChecklistModel { get; set; }
}
