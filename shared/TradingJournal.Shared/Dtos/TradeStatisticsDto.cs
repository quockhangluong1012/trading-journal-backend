namespace TradingJournal.Shared.Dtos;

/// <summary>
/// Pre-aggregated trade statistics computed at the database level.
/// </summary>
public sealed class TradeStatisticsDto
{
    public decimal TotalPnl { get; init; }
    public int WinCount { get; init; }
    public int LossCount { get; init; }
    public int OpenPositions { get; init; }
    public int TotalTrades { get; init; }
    public int ClosedTrades { get; init; }
}
