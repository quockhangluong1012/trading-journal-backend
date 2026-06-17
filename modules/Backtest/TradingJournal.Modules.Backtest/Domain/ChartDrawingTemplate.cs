using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Backtest.Domain;

[Table("ChartDrawingTemplates", Schema = "Backtest")]
public sealed class ChartDrawingTemplate : EntityBase<int>
{
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string StyleJson { get; set; } = "{}";

    public string? Tool { get; set; }

    public string? Text { get; set; }
}
