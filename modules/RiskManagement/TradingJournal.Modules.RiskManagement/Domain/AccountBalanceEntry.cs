using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.RiskManagement.Domain;

[Table(name: "AccountBalanceEntries", Schema = "Risk")]
public sealed class AccountBalanceEntry : EntityBase<int>
{
    /// <summary>
    /// The type of balance entry (deposit, withdrawal, adjustment).
    /// </summary>
    public BalanceEntryType EntryType { get; set; }

    /// <summary>
    /// The amount of the deposit/withdrawal. Always positive; direction is determined by EntryType.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The resulting account balance after this entry.
    /// </summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>
    /// Optional notes for this entry (e.g., "Monthly deposit", "Prop firm payout").
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// The date of this balance entry.
    /// </summary>
    public DateTimeOffset EntryDate { get; set; }
}
