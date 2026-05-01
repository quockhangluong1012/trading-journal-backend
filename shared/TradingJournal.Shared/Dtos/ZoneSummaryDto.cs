namespace TradingJournal.Shared.Dtos;

public class ZoneSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FromTime { get; set; } = string.Empty;
    public string ToTime { get; set; } = string.Empty;
    public string? Description { get; set; }
}
