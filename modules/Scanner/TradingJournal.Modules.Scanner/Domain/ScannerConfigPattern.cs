using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Scanner.Domain;

/// <summary>
/// Join entity linking a ScannerConfig to an enabled ICT pattern type.
/// </summary>
[Table("ScannerConfigPatterns", Schema = "Scanner")]
[Index(nameof(ScannerConfigId), nameof(PatternType), IsUnique = true)]
public sealed class ScannerConfigPattern : EntityBase<int>
{
    public int ScannerConfigId { get; set; }
    public ScannerConfig ScannerConfig { get; set; } = null!;

    public IctPatternType PatternType { get; set; }
}
