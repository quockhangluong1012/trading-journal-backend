using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.ViewModel;

public class TradeHistoryViewModel
{
    public int Id { get; set; }

    public string Asset { get; set; } = string.Empty;

    public PositionType Position { get; set; }

    public double EntryPrice { get; set; }

    public DateTime Date { get; set; }

    public TradeStatus Status { get; set; }

    public double? ExitPrice { get; set; }

    public double? Pnl { get; set; }

    public DateTime? ClosedDate { get; set; }

    public List<EmotionTagCacheDto>? EmotionTags { get; set; }

    public ConfidenceLevel ConfidenceLevel { get; set; }
}