using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.Trades;

internal sealed class TechnicalAnalysisTagProvider(ITradeDbContext context) : ITechnicalAnalysisTagProvider
{
    public async Task<List<TechnicalAnalysisTagDto>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        return await context.TechnicalAnalyses
            .AsNoTracking()
            .Select(t => new TechnicalAnalysisTagDto
            {
                Id = t.Id,
                Name = t.Name,
                ShortName = t.ShortName,
            })
            .ToListAsync(cancellationToken);
    }
}
