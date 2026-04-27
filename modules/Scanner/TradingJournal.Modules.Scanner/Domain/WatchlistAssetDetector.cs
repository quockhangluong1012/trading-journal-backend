using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Scanner.Domain;

/// <summary>
/// Join entity linking a WatchlistAsset to an enabled ICT detector.
/// When present, overrides the user's global ScannerConfig.EnabledPatterns for this asset.
/// </summary>
[Table("WatchlistAssetDetectors", Schema = "Scanner")]
[Index(nameof(WatchlistAssetId), nameof(PatternType), IsUnique = true)]
public sealed class WatchlistAssetDetector : EntityBase<int>
{
    public int WatchlistAssetId { get; set; }
    public WatchlistAsset WatchlistAsset { get; set; } = null!;

    public IctPatternType PatternType { get; set; }

    public bool IsEnabled { get; set; } = true;
}
