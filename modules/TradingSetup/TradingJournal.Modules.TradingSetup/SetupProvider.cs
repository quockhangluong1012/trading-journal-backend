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
}

