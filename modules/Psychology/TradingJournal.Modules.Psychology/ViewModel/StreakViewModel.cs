namespace TradingJournal.Modules.Psychology.ViewModel;

public sealed class StreakViewModel
{
    public string StreakType { get; set; } = string.Empty;
    public int Length { get; set; }
    public decimal StreakPnl { get; set; }
    public int BestWinStreak { get; set; }
    public int WorstLossStreak { get; set; }
    public int TotalClosedTrades { get; set; }
    public DateTime RecordedAt { get; set; }
}
