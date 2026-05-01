using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface ITechnicalAnalysisTagProvider
{
    Task<List<TechnicalAnalysisTagDto>> GetTagsAsync(CancellationToken cancellationToken = default);
}
