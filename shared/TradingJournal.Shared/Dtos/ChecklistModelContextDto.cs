namespace TradingJournal.Shared.Dtos;

public sealed record ChecklistCriterionContextDto(
    int Id,
    string Name,
    int Type,
    string Category);

public sealed record ChecklistModelContextDto(
    int Id,
    string Name,
    string? Description,
    IReadOnlyCollection<ChecklistCriterionContextDto> Criteria);