namespace TradingJournal.Modules.Psychology.ViewModel;

public sealed class DailyNoteViewModel
{
    public int Id { get; set; }
    public DateTimeOffset NoteDate { get; set; }
    public string DailyBias { get; set; } = string.Empty;
    public string MarketStructureNotes { get; set; } = string.Empty;
    public string KeyLevelsAndLiquidity { get; set; } = string.Empty;
    public string NewsAndEvents { get; set; } = string.Empty;
    public string SessionFocus { get; set; } = string.Empty;
    public string RiskAppetite { get; set; } = string.Empty;
    public string MentalState { get; set; } = string.Empty;
    public string KeyRulesAndReminders { get; set; } = string.Empty;
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }
}
