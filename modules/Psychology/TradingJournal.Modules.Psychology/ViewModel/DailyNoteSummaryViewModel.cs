namespace TradingJournal.Modules.Psychology.ViewModel;

/// <summary>
/// Lightweight view model for daily note list items (without full text fields).
/// </summary>
public sealed class DailyNoteSummaryViewModel
{
    public int Id { get; set; }
    public DateOnly NoteDate { get; set; }
    public string DailyBias { get; set; } = string.Empty;
    public string SessionFocus { get; set; } = string.Empty;
    public string RiskAppetite { get; set; } = string.Empty;
    public string MentalState { get; set; } = string.Empty;

    /// <summary>
    /// Number of fields filled (out of 8 total).
    /// </summary>
    public int FilledFieldsCount { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
