namespace TradingJournal.Modules.Trades.ViewModel;

public sealed class TradingZoneViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; } = string.Empty;

    public string FromTime { get; set; } = string.Empty;
    
    public string ToTime { get; set; } = string.Empty;
}