namespace TradingJournal.Modules.Trades.ViewModel;

public class DisciplineLogViewModel
{
    public int Id { get; set; }

    public int DisciplineRuleId { get; set; }

    public string RuleName { get; set; } = string.Empty;

    public int? TradeHistoryId { get; set; }

    public string? TradeAsset { get; set; }

    public bool WasFollowed { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset Date { get; set; }
}
