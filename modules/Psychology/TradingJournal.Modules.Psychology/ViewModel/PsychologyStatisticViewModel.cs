namespace TradingJournal.Modules.Psychology.ViewModel;

public sealed class PsychologyStatisticViewModel
{
    public double AvgConfidence { get; set; }

    public string TopEmotion { get; set; } = string.Empty;

    public double PsychologyScore { get; set; }

    public int JournalEntries { get; set; }
}
