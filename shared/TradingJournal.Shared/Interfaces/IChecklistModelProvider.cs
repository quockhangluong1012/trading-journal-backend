using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface IChecklistModelProvider
{
    Task<ChecklistModelContextDto?> GetChecklistModelAsync(int userId, int checklistModelId, CancellationToken cancellationToken = default);
}