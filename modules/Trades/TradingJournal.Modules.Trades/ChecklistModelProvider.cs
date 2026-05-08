using Microsoft.EntityFrameworkCore;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.Trades;

internal sealed class ChecklistModelProvider(ITradeDbContext context) : IChecklistModelProvider
{
    public async Task<ChecklistModelContextDto?> GetChecklistModelAsync(int userId, int checklistModelId, CancellationToken cancellationToken = default)
    {
        var checklistModel = await context.ChecklistModels
            .AsNoTracking()
            .Where(model => model.Id == checklistModelId && model.CreatedBy == userId)
            .Select(model => new ChecklistModelContextDto(
                model.Id,
                model.Name,
                model.Description,
                model.Criteria
                    .OrderBy(criteria => criteria.CheckListType)
                    .ThenBy(criteria => criteria.Name)
                    .Select(criteria => new ChecklistCriterionContextDto(
                        criteria.Id,
                        criteria.Name,
                        (int)criteria.CheckListType,
                        string.Empty))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return checklistModel is null
            ? null
            : checklistModel with
            {
                Criteria = [.. checklistModel.Criteria.Select(criteria => criteria with { Category = GetCategoryLabel((PretradeChecklistType)criteria.Type) })]
            };
    }

    private static string GetCategoryLabel(PretradeChecklistType type)
    {
        return type switch
        {
            PretradeChecklistType.MarketStructure => "Market Structure",
            PretradeChecklistType.TradingSetup => "Trade Setup",
            PretradeChecklistType.RiskManagement => "Risk Management",
            PretradeChecklistType.Psychology => "Psychology",
            _ => "Other"
        };
    }
}