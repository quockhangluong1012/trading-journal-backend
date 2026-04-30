using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.RiskManagement.Domain;

[Table(name: "RiskConfigs", Schema = "Risk")]
public sealed class RiskConfig : EntityBase<int>
{
    /// <summary>
    /// Maximum daily loss allowed as a percentage of account balance (e.g., 2.0 = 2%).
    /// </summary>
    public decimal DailyLossLimitPercent { get; set; } = 2.0m;

    /// <summary>
    /// Maximum weekly drawdown allowed as a percentage of account balance (e.g., 5.0 = 5%).
    /// </summary>
    public decimal WeeklyDrawdownCapPercent { get; set; } = 5.0m;

    /// <summary>
    /// Default risk per trade as a percentage of account balance (e.g., 1.0 = 1%).
    /// </summary>
    public decimal RiskPerTradePercent { get; set; } = 1.0m;

    /// <summary>
    /// Maximum number of concurrent open positions allowed.
    /// </summary>
    public int MaxOpenPositions { get; set; } = 5;

    /// <summary>
    /// Maximum correlated exposure allowed (e.g., 3 = max 3 correlated positions).
    /// </summary>
    public int MaxCorrelatedPositions { get; set; } = 3;

    /// <summary>
    /// Current account balance used for position sizing calculations.
    /// Updated when account balance entries are added.
    /// </summary>
    public decimal AccountBalance { get; set; } = 10000m;
}
