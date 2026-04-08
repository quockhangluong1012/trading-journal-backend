using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "ChecklistModels", Schema = "Trades")]
public sealed class ChecklistModel : EntityBase<int>
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<PretradeChecklist> Criteria { get; set; } = [];
}
