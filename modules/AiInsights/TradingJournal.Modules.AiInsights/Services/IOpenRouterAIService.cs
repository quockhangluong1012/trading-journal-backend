using TradingJournal.Modules.AiInsights.Dto;

namespace TradingJournal.Modules.AiInsights.Services;

public interface IOpenRouterAIService
{
    Task<TradeAnalysisResultDto?> GenerateTradingOrderSummary(int tradeHistoryId, CancellationToken cancellationToken);

    Task<ReviewSummaryResultDto?> GenerateReviewSummary(ReviewSummaryRequestDto request, CancellationToken cancellationToken);

    Task<AiCoachResponseDto> ChatWithCoachAsync(AiCoachRequestDto request, CancellationToken cancellationToken);

    Task<PreTradeValidationResultDto?> ValidateTradeSetupAsync(PreTradeValidationRequestDto request, CancellationToken cancellationToken);

    Task<EmotionDetectionResultDto?> DetectEmotionsAsync(EmotionDetectionRequestDto request, CancellationToken cancellationToken);

    Task<MorningBriefingResultDto?> GenerateMorningBriefingAsync(MorningBriefingRequestDto request, CancellationToken cancellationToken);
}
