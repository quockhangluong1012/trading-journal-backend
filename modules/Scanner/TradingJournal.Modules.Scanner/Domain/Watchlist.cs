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

    public ICollection<WatchlistAsset> Assets { get; set; } = [];
}
