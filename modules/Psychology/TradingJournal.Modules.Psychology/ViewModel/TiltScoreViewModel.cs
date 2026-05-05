namespace TradingJournal.Modules.Psychology.ViewModel;

public sealed class TiltScoreViewModel
{
    public int Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public int ConsecutiveLosses { get; set; }
    public int ConsecutiveWins { get; set; }
    public int TradesLastHour { get; set; }
    public int RuleBreaksToday { get; set; }
    public decimal TodayPnl { get; set; }
    public bool CircuitBreakerTriggered { get; set; }
    public DateTime? CooldownUntil { get; set; }
    public DateTime RecordedAt { get; set; }
}
