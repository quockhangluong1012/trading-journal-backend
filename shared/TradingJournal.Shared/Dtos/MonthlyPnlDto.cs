namespace TradingJournal.Shared.Dtos;

/// <summary>
/// Monthly PnL aggregate computed at the database level.
/// </summary>
public sealed class MonthlyPnlDto
{
    public string Month { get; init; } = string.Empty;
    public decimal Pnl { get; init; }
}
