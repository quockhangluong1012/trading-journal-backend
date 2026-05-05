using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Psychology.Domain;


[Table(name: "PsychologyJournals", Schema = "Psychology")]
public sealed class PsychologyJournal : EntityBase<int>
{
    public DateTime Date { get; set; }

    public OverallMood OverallMood { get; set; } = OverallMood.Neutral;

    public ConfidentLevel ConfidentLevel { get; set; } = ConfidentLevel.None;

    public string TodayTradingReview { get; set; } = string.Empty;

    public ICollection<PsychologyJournalEmotion> PsychologyJournalEmotions { get; set; } = [];
}