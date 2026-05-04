namespace TradingJournal.Modules.Psychology.ViewModel;

public class EmotionFrequencyViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class EmotionWinRateViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int WinRate { get; set; }
    public int Total { get; set; }
}

public class EmotionDistributionViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Fill { get; set; } = string.Empty;
}

public class MoodAndConfidenceTrendViewModel
{
    public DateTimeOffset Date { get; set; }
    public int Mood { get; set; }
    public int Confidence { get; set; }
}

public class PsychologyHeatmapViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal AvgPnl { get; set; }
    public decimal TotalPnl { get; set; }
    public int Count { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int WinRate { get; set; }
    public decimal AvgWinPnl { get; set; }
    public decimal AvgLossPnl { get; set; }
}
