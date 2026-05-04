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

    #region Playbook Rules

    /// <summary>
    /// Structured entry rules for this playbook (e.g., "Wait for FVG fill on 15m after BOS on 1H").
    /// </summary>
    public string? EntryRules { get; set; }

    /// <summary>
    /// Structured exit rules for this playbook (e.g., "TP at next liquidity pool, trail after 1R").
    /// </summary>
    public string? ExitRules { get; set; }

    /// <summary>
    /// Ideal market conditions where this setup performs best (e.g., "Trending, London/NY overlap").
    /// </summary>
    public string? IdealMarketConditions { get; set; }

    /// <summary>
    /// Suggested risk percentage per trade when using this setup.
    /// </summary>
    public decimal? RiskPerTrade { get; set; }

    /// <summary>
    /// Target risk-to-reward ratio for this setup.
    /// </summary>
    public decimal? TargetRiskReward { get; set; }

    /// <summary>
    /// Comma-separated preferred timeframes (e.g., "15m,1H,4H").
    /// </summary>
    public string? PreferredTimeframes { get; set; }

    /// <summary>
    /// Comma-separated preferred assets (e.g., "EUR/USD,GBP/USD,XAU/USD").
    /// </summary>
    public string? PreferredAssets { get; set; }

    #endregion

    #region Retirement / Kill Switch

    /// <summary>
    /// Reason for retiring this setup (kill switch). Populated when Status == Retired.
    /// </summary>
    public string? RetiredReason { get; set; }

    /// <summary>
    /// Date when this setup was retired.
    /// </summary>
    public DateTimeOffset? RetiredDate { get; set; }

    #endregion

    public ICollection<SetupStep> Steps { get; set; } = [];

    public ICollection<SetupConnection> Connections { get; set; } = [];
}
