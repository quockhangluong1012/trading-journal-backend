using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Shared.Dtos;

public class TradeCacheDto
{
    public int Id { get; set; }
    public string Asset { get; set; } = string.Empty;
    public PositionType Position { get; set; }
    public double EntryPrice { get; set; }
    public double? ExitPrice { get; set; }
    public double StopLoss { get; set; }
    public double TargetTier1 { get; set; }
    public TradeStatus Status { get; set; }
    public DateTime Date { get; set; }
    public decimal? Pnl { get; set; }
    public DateTime? ClosedDate { get; set; }
    public int? TradingSessionId { get; set; }
    public int? TradingZoneId { get; set; }
    public List<int>? EmotionTags { get; set; }
    public int CreatedBy { get; set; }
}
