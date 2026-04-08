namespace TradingJournal.Modules.Trades.ViewModel;

public sealed record TradingStatisticViewModel
{
    public double TotalPnL { get; set; }

    public double WinRate { get; set; }

    public int TotalTrades { get; set; }

    public int OpenPositions { get; set; }
}