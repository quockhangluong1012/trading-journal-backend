using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Setups.Domain;

[Table(name: "SetupConnections", Schema = "Setups")]
public sealed class SetupConnection : EntityBase<int>
{
    public int TradingSetupId { get; set; }

    public int SourceStepId { get; set; }

    public int TargetStepId { get; set; }

    public string? Label { get; set; }

    public bool IsAnimated { get; set; }

    public string? Color { get; set; }

    public SetupStep SourceStep { get; set; } = null!;

    public SetupStep TargetStep { get; set; } = null!;

    public TradingSetup TradingSetup { get; set; } = null!;
}
