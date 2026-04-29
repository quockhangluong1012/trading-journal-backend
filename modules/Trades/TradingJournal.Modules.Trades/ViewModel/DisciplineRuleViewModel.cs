namespace TradingJournal.Modules.Trades.ViewModel;

public class DisciplineRuleViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public LessonCategory Category { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedDate { get; set; }
}
