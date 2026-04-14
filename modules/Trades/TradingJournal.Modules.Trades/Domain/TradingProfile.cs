using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Trades.Domain;

[Table(name: "TradingProfiles", Schema = "Trades")]
public sealed class TradingProfile : EntityBase<int>
{
    public int? MaxTradesPerDay { get; set; }

    public double? MaxDailyLossPercentage { get; set; }

    public int? MaxConsecutiveLosses { get; set; }

    public bool IsDisciplineEnabled { get; set; } = true;
}
