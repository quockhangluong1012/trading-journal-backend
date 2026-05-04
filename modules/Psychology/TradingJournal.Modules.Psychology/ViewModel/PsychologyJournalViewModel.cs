namespace TradingJournal.Modules.Psychology.ViewModel;

public class PsychologyJournalViewModel
{
    public int Id { get; set; }
    
    public DateTimeOffset Date { get; set; }

    public string TodayTradingReview { get; set; } = string.Empty;

    public OverallMood OverallMood { get; set; }

    public ConfidentLevel ConfidentLevel { get; set; }

    public List<PsychologyJournalEmotionViewModel> EmotionTags { get; set; } = [];
}

public class PsychologyJournalEmotionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}