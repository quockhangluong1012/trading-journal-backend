using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradingZones", Schema = "Trades")]
public sealed class TradingZone : EntityBase<int>
{
    public string Name { get; set; } = string.Empty;

    public string FromTime { get; set; } = string.Empty;

    public string ToTime { get; set; } = string.Empty;

    public string? Description { get; set; } = string.Empty;
}
