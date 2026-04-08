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
}
