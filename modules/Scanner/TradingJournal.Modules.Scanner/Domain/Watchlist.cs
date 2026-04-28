using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Scanner.Domain;

[Table("Watchlists", Schema = "Scanner")]
public sealed class Watchlist : EntityBase<int>
{
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int UserId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether the scanner engine is actively running for this watchlist.
    /// Persisted in DB so scanning survives page refreshes and reconnections.
    /// </summary>
    public bool IsScannerRunning { get; set; } = false;

    public ICollection<WatchlistAsset> Assets { get; set; } = [];
}
