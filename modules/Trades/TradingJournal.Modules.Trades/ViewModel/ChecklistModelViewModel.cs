namespace TradingJournal.Modules.Trades.ViewModel;

public sealed record ChecklistModelViewModel(int Id, string Name, string? Description, int CriteriaCount);

public sealed record ChecklistModelDetailViewModel(
    int Id,
    string Name,
    string? Description,
    IReadOnlyCollection<PretradeChecklistViewModel> Criteria);
