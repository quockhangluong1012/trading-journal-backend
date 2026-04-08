using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Psychology.Domain;


[Table(name: "PsychologyJournalEmotions", Schema = "Psychology")]
public sealed class PsychologyJournalEmotion : EntityBase<int>
{
    public int PsychologyJournalId { get; set; }

    public int EmotionTagId { get; set; }

    [ForeignKey(nameof(PsychologyJournalId))]
    public PsychologyJournal PsychologyJournal { get; set; } = null!;

    [ForeignKey(nameof(EmotionTagId))]
    public EmotionTag EmotionTag { get; set; } = null!;
}
