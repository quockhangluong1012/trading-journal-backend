using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Modules.Trades;

internal sealed class ZoneProvider(ITradeDbContext context) : IZoneProvider
{
    public async Task<List<ZoneSummaryDto>> GetZonesAsync(CancellationToken cancellationToken = default)
    {
        return await context.TradingZones
            .AsNoTracking()
            .Select(z => new ZoneSummaryDto
            {
                Id = z.Id,
                Name = z.Name,
                FromTime = z.FromTime,
                ToTime = z.ToTime,
                Description = z.Description,
            })
            .ToListAsync(cancellationToken);
    }
}
