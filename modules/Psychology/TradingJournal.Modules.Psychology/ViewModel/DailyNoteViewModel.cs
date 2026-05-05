namespace TradingJournal.Modules.Psychology.ViewModel;

public sealed class DailyNoteViewModel
{
    public int Id { get; set; }
    public DateOnly NoteDate { get; set; }
    public string DailyBias { get; set; } = string.Empty;
    public string MarketStructureNotes { get; set; } = string.Empty;
    public string KeyLevelsAndLiquidity { get; set; } = string.Empty;
    public string NewsAndEvents { get; set; } = string.Empty;
    public string SessionFocus { get; set; } = string.Empty;
    public string RiskAppetite { get; set; } = string.Empty;
    public string MentalState { get; set; } = string.Empty;
    public string KeyRulesAndReminders { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
