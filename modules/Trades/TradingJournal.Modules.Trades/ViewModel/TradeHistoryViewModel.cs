using TradingJournal.Shared.Dtos;

namespace TradingJournal.Modules.Trades.ViewModel;

public class TradeHistoryViewModel
{
    public int Id { get; set; }

    public string Asset { get; set; } = string.Empty;

    public PositionType Position { get; set; }

    public decimal EntryPrice { get; set; }

    public DateTime Date { get; set; }

    public TradeStatus Status { get; set; }

    public decimal? ExitPrice { get; set; }

    public decimal? Pnl { get; set; }

    public DateTime? ClosedDate { get; set; }

    public List<EmotionTagCacheDto>? EmotionTags { get; set; }

    public ConfidenceLevel ConfidenceLevel { get; set; }

    public PowerOf3Phase? PowerOf3Phase { get; set; }

    public DailyBias? DailyBias { get; set; }

    public MarketStructure? MarketStructure { get; set; }

    public PremiumDiscount? PremiumDiscount { get; set; }
}