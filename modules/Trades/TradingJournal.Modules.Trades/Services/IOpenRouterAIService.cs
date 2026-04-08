using TradingJournal.Modules.Trades.Dto;

namespace TradingJournal.Modules.Trades.Services;

public interface IOpenRouterAIService
{
    Task<TradeAnalysisResultDto?> GenerateTradingOrderSummary(int tradeHistoryId, CancellationToken cancellationToken);

    Task<ReviewSummaryResultDto?> GenerateReviewSummary(ReviewSummaryRequestDto request, CancellationToken cancellationToken);
}
