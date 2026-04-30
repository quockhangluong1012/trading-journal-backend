namespace TradingJournal.Shared.Dtos;

public class SetupSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; }
}
