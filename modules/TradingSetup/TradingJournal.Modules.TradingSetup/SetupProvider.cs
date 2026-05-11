using TradingJournal.Modules.Setups.Infrastructure;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TradingJournal.Modules.Setups;

internal sealed class SetupProvider(ISetupDbContext context) : ISetupProvider
{
    public async Task<List<SetupSummaryDto>> GetSetupsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await context.TradingSetups
            .AsNoTracking()
            .Where(s => s.CreatedBy == userId)
            .Select(s => new SetupSummaryDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                Status = (int)s.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PlaybookKnowledgeItemDto>> GetPlaybookKnowledgeItemsAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await context.TradingSetups
            .AsNoTracking()
            .Where(setup => setup.CreatedBy == userId && !setup.IsDisabled)
            .OrderBy(setup => setup.Status)
            .ThenByDescending(setup => setup.UpdatedDate ?? setup.CreatedDate)
            .Select(setup => new PlaybookKnowledgeItemDto
            {
                SetupId = setup.Id,
                Name = setup.Name,
                Description = setup.Description,
                Status = setup.Status.ToString(),
                EntryRules = setup.EntryRules,
                ExitRules = setup.ExitRules,
                IdealMarketConditions = setup.IdealMarketConditions,
                RiskPerTrade = setup.RiskPerTrade,
                TargetRiskReward = setup.TargetRiskReward,
                PreferredTimeframes = setup.PreferredTimeframes,
                PreferredAssets = setup.PreferredAssets,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasSetupAsync(int userId, int setupId, CancellationToken cancellationToken = default)
    {
        return context.TradingSetups
            .AsNoTracking()
            .AnyAsync(setup => setup.Id == setupId && setup.CreatedBy == userId, cancellationToken);
    }
}

