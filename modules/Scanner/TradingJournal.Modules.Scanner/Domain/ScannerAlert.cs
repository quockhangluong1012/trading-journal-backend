using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Scanner.Domain;

[Table("ScannerAlerts", Schema = "Scanner")]
[Index(nameof(UserId), nameof(DetectedAt))]
public sealed class ScannerAlert : EntityBase<int>
{
    public int UserId { get; set; }

    [MaxLength(30)]
    public string Symbol { get; set; } = string.Empty;

    public IctPatternType PatternType { get; set; }

    public ScannerTimeframe Timeframe { get; set; }

    /// <summary>
    /// The timeframe where the pattern was detected.
    /// </summary>
    public ScannerTimeframe DetectionTimeframe { get; set; }

    [Column(TypeName = "decimal(28,10)")]
    public decimal PriceAtDetection { get; set; }

    [Column(TypeName = "decimal(28,10)")]
    public decimal? ZoneHighPrice { get; set; }

    [Column(TypeName = "decimal(28,10)")]
    public decimal? ZoneLowPrice { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public TradingJournal.Modules.Scanner.Common.Enums.MarketRegime Regime { get; set; }

    /// <summary>
    /// Multi-timeframe confluence score (higher = more timeframes confirm).
    /// </summary>
    public int ConfluenceScore { get; set; }

    public DateTime DetectedAt { get; set; }

    public bool IsDismissed { get; set; } = false;
}
