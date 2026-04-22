namespace TradingJournal.Modules.Trades.ViewModel;

public sealed record TradingStatisticViewModel
{
    public decimal TotalPnL { get; set; }

    public decimal WinRate { get; set; }

    public int TotalTrades { get; set; }

    public int OpenPositions { get; set; }
}