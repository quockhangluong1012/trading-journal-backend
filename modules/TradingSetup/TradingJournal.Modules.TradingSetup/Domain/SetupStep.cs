using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Setups.Domain;

[Table(name: "SetupSteps", Schema = "Setups")]
public sealed class SetupStep : EntityBase<int>
{
    public int TradingSetupId { get; set; }

    public int StepNumber { get; set; }

    public string Label { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string NodeType { get; set; } = string.Empty;

    public string? Color { get; set; }

    public double PositionX { get; set; }

    public double PositionY { get; set; }

    public TradingSetup TradingSetup { get; set; } = null!;
}
