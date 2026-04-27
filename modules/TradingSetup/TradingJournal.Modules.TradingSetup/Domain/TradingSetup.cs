using System.ComponentModel.DataAnnotations.Schema;
using TradingJournal.Modules.Setups.Common.Enum;

namespace TradingJournal.Modules.Setups.Domain;

[Table(name: "TradingSetups", Schema = "Setups")]
public sealed class TradingSetup : EntityBase<int>
{
    public string Name { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? Description { get; set; }

    public SetupStatus Status { get; set; } = SetupStatus.Active;

    public string? Notes { get; set; }

    public ICollection<SetupStep> Steps { get; set; } = [];

    public ICollection<SetupConnection> Connections { get; set; } = [];
}
