using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Scanner.Domain;

[Table("WatchlistAssets", Schema = "Scanner")]
[Index(nameof(WatchlistId), nameof(Symbol), IsUnique = true)]
public sealed class WatchlistAsset : EntityBase<int>
{
    public int WatchlistId { get; set; }
    public Watchlist Watchlist { get; set; } = null!;

    [MaxLength(30)]
    public string Symbol { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Per-asset detector overrides. When populated, these override the user's global
    /// ScannerConfig.EnabledPatterns for this specific asset.
    /// </summary>
    public ICollection<WatchlistAssetDetector> EnabledDetectors { get; set; } = [];
}
