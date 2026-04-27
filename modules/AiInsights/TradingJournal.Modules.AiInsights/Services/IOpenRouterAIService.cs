using TradingJournal.Modules.AiInsights.Dto;

namespace TradingJournal.Modules.AiInsights.Services;

public interface IOpenRouterAIService
{
    Task<TradeAnalysisResultDto?> GenerateTradingOrderSummary(int tradeHistoryId, CancellationToken cancellationToken);

    Task<ReviewSummaryResultDto?> GenerateReviewSummary(ReviewSummaryRequestDto request, CancellationToken cancellationToken);

    Task<AiCoachResponseDto> ChatWithCoachAsync(AiCoachRequestDto request, CancellationToken cancellationToken);
}
