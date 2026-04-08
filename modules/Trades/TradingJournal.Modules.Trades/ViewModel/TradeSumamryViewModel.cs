namespace TradingJournal.Modules.Trades.ViewModel;

public sealed class TradeSumamryViewModel
{
    public int TradeId { get; set; }

    public string ExecutiveSummary { get; set; } = string.Empty;

    public string TechnicalInsights { get; set; } = string.Empty;

    public string PsychologyAnalysis { get; set; } = string.Empty;

    public CriticalMistakesViewModel CriticalMistakes { get; set; } = new();
}

public sealed class CriticalMistakesViewModel
{
    public List<string> Technical { get; set; } = [];

    public List<string> Psychological { get; set; } = [];
}
