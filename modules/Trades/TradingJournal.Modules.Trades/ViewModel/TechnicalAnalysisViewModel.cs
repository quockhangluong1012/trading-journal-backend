namespace TradingJournal.Modules.Trades.ViewModel;

public sealed class TechnicalAnalysisViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public string? Description { get; set; }
}