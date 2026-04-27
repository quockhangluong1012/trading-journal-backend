using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Scanner.Domain;

/// <summary>
/// Join entity linking a ScannerConfig to an enabled scanner timeframe.
/// </summary>
[Table("ScannerConfigTimeframes", Schema = "Scanner")]
[Index(nameof(ScannerConfigId), nameof(Timeframe), IsUnique = true)]
public sealed class ScannerConfigTimeframe : EntityBase<int>
{
    public int ScannerConfigId { get; set; }
    public ScannerConfig ScannerConfig { get; set; } = null!;

    public ScannerTimeframe Timeframe { get; set; }
}
