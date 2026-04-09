using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Backtest.Domain;

/// <summary>
/// Represents an asset that has been registered by an admin for backtesting.
/// Tracks sync status and progress for M1 candle data.
///
/// Only M1 candles are stored — higher timeframes (M5, M15, H1, H4, D1)
/// are computed on-the-fly via the CandleAggregationService.
/// </summary>
[Table("BacktestAssets", Schema = "Backtest")]
[Index(nameof(Symbol), IsUnique = true)]
public sealed class BacktestAsset : EntityBase<int>
{
    /// <summary>
    /// Display name shown in the UI (e.g., "EUR/USD", "NQ E-Mini", "Gold").
    /// </summary>
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Normalized symbol used for API calls and storage lookup.
    /// For Twelve Data: "EUR/USD", "XAU/USD", "NQ"
    /// For imported CSV data: matches the CSV source format.
    /// </summary>
    [MaxLength(30)]
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// Asset category for UI grouping.
    /// </summary>
    [MaxLength(30)]
    public string Category { get; set; } = string.Empty; // "Forex", "Metals", "Futures", "Crypto"

    public AssetSyncStatus SyncStatus { get; set; } = AssetSyncStatus.Pending;

    /// <summary>
    /// Data provider used for syncing: "TwelveData", "AlphaVantage", "CSV".
    /// </summary>
    [MaxLength(30)]
    public string DataProvider { get; set; } = "TwelveData";

    /// <summary>
    /// Start date of the desired data range (inclusive).
    /// </summary>
    public DateTime DataStartDate { get; set; }

    /// <summary>
    /// End date of the desired data range. Null means sync up to current date.
    /// </summary>
    public DateTime? DataEndDate { get; set; }

    /// <summary>
    /// The last month that has been successfully synced (for incremental sync resumption).
    /// Format: the last timestamp of the last synced candle.
    /// </summary>
    public DateTime? LastSyncedDate { get; set; }

    /// <summary>
    /// Total M1 candles stored in the database for this asset.
    /// </summary>
    public long TotalCandles { get; set; }

    /// <summary>
    /// Error message from the last failed sync attempt.
    /// </summary>
    [MaxLength(500)]
    public string? LastError { get; set; }

    /// <summary>
    /// Default spread in pips for this asset (e.g., 1.5 for EUR/USD, 30 for XAU/USD).
    /// Applied to new backtest sessions unless overridden.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal DefaultSpreadPips { get; set; }

    /// <summary>
    /// Size of 1 pip in price terms. Determines how pips convert to actual price.
    /// Forex majors: 0.0001, JPY pairs: 0.01, Gold: 0.01, Indices: 1.0
    /// </summary>
    [Column(TypeName = "decimal(18,10)")]
    public decimal PipSize { get; set; } = 0.0001m;
}
