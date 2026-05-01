using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface IZoneProvider
{
    Task<List<ZoneSummaryDto>> GetZonesAsync(CancellationToken cancellationToken = default);
}
