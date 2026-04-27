using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Scanner.Domain;

[Table("ScannerConfigs", Schema = "Scanner")]
[Index(nameof(UserId), IsUnique = true)]
public sealed class ScannerConfig : EntityBase<int>
{
    public int UserId { get; set; }

    /// <summary>
    /// Scan interval in seconds (minimum 60).
    /// </summary>
    public int ScanIntervalSeconds { get; set; } = 300; // 5 minutes default

    /// <summary>
    /// Minimum confluence score to trigger notification (1-4).
    /// </summary>
    public int MinConfluenceScore { get; set; } = 1;

    /// <summary>
    /// Whether the scanner is currently active for this user.
    /// </summary>
    public bool IsRunning { get; set; } = false;

    /// <summary>
    /// Enabled ICT pattern types for this user's scanner.
    /// </summary>
    public ICollection<ScannerConfigPattern> EnabledPatterns { get; set; } = [];

    /// <summary>
    /// Enabled timeframes for this user's scanner.
    /// </summary>
    public ICollection<ScannerConfigTimeframe> EnabledTimeframes { get; set; } = [];
}
