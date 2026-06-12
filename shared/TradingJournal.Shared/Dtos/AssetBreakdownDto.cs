namespace TradingJournal.Shared.Dtos;

/// <summary>
/// Per-asset breakdown computed at the database level.
/// </summary>
public sealed class AssetBreakdownDto
{
    public string Asset { get; init; } = string.Empty;
    public decimal TotalPnl { get; init; }
    public int TradeCount { get; init; }
    public int WinCount { get; init; }
}
