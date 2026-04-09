using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Backtest.Domain;

/// <summary>
/// Represents a queued CSV file that needs to be imported into the candle database.
/// Created when an admin uploads one or more CSV files via the bulk-import endpoint.
/// Processed sequentially by the CsvImportBackgroundService.
/// </summary>
[Table("CsvImportJobs", Schema = "Backtest")]
[Index(nameof(Status), nameof(CreatedDate), Name = "IX_CsvImportJobs_StatusCreated")]
public sealed class CsvImportJob : EntityBase<int>
{
    /// <summary>
    /// The asset this import is for.
    /// </summary>
    public int AssetId { get; set; }

    /// <summary>
    /// Original file name as uploaded by the user.
    /// </summary>
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Path to the stored CSV file on disk (temp storage until processing completes).
    /// </summary>
    [MaxLength(500)]
    public string StoredFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Current processing status of this import job.
    /// </summary>
    public CsvImportStatus Status { get; set; } = CsvImportStatus.Pending;

    /// <summary>
    /// Number of candles successfully imported from this file.
    /// </summary>
    public int ImportedCandles { get; set; }

    /// <summary>
    /// Number of duplicate candles that were skipped.
    /// </summary>
    public int SkippedDuplicates { get; set; }

    /// <summary>
    /// Error message if the import failed.
    /// </summary>
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the job finished processing (success or failure).
    /// </summary>
    public DateTime? ProcessedDate { get; set; }

    /// <summary>
    /// Navigation property for the parent asset.
    /// </summary>
    [ForeignKey(nameof(AssetId))]
    public BacktestAsset? Asset { get; set; }
}
