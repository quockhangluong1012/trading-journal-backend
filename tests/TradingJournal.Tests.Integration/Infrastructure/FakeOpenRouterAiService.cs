using System.Threading;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Integration.Infrastructure;

public sealed class FakeOpenRouterAiService : IOpenRouterAIService
{
    private int _analyzeChartScreenshotCalls;

    public int AnalyzeChartScreenshotCalls => _analyzeChartScreenshotCalls;

    public void Reset()
    {
        Interlocked.Exchange(ref _analyzeChartScreenshotCalls, 0);
    }

    public Task<TradeAnalysisResultDto?> GenerateTradingOrderSummary(int tradeHistoryId, CancellationToken cancellationToken)
    {
        return Task.FromResult<TradeAnalysisResultDto?>(null);
    }

    public Task<ReviewSummaryResultDto?> GenerateReviewSummary(ReviewSummaryRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<ReviewSummaryResultDto?>(null);
    }

    public Task<AiCoachResponseDto> ChatWithCoachAsync(AiCoachRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new AiCoachResponseDto("fake-response"));
    }

    public Task<PreTradeValidationResultDto?> ValidateTradeSetupAsync(PreTradeValidationRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<PreTradeValidationResultDto?>(null);
    }

    public Task<ChartScreenshotAnalysisResultDto?> AnalyzeChartScreenshotAsync(ChartScreenshotAnalysisRequestDto request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _analyzeChartScreenshotCalls);

        return Task.FromResult<ChartScreenshotAnalysisResultDto?>(new ChartScreenshotAnalysisResultDto
        {
            Summary = "Fake analysis",
            MarketStructure = "Bullish structure",
            AmdPhase = "Expansion",
            PremiumDiscount = "Discount",
            ConfidenceScore = 0.8m,
            KeyLevels = ["1.1000"],
            DetectedConfluences = ["Support"],
            Warnings = [],
            SuggestedActions = ["Wait for confirmation"]
        });
    }

    public Task<EmotionDetectionResultDto?> DetectEmotionsAsync(EmotionDetectionRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<EmotionDetectionResultDto?>(null);
    }

    public Task<AiRiskAdvisorResultDto?> GenerateRiskAdvisorAsync(AiRiskAdvisorRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<AiRiskAdvisorResultDto?>(null);
    }

    public Task<AiWeeklyDigestResultDto?> GenerateWeeklyDigestAsync(AiWeeklyDigestRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<AiWeeklyDigestResultDto?>(null);
    }

    public Task<AiEconomicImpactPredictorResultDto?> GenerateEconomicImpactPredictionAsync(AiEconomicImpactPredictorRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<AiEconomicImpactPredictorResultDto?>(null);
    }

    public Task<MorningBriefingResultDto?> GenerateMorningBriefingAsync(MorningBriefingRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<MorningBriefingResultDto?>(null);
    }

    public Task<NaturalLanguageTradeSearchResultDto?> SearchTradesNaturalLanguageAsync(NaturalLanguageTradeSearchRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<NaturalLanguageTradeSearchResultDto?>(null);
    }

    public Task<TradePatternDiscoveryResultDto?> DiscoverTradePatternsAsync(TradePatternDiscoveryRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<TradePatternDiscoveryResultDto?>(null);
    }

    public Task<SuggestedLessonsResultDto?> SuggestLessonsAsync(SuggestLessonsRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<SuggestedLessonsResultDto?>(null);
    }

    public Task<PlaybookOptimizationResultDto?> OptimizePlaybookAsync(PlaybookOptimizationRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<PlaybookOptimizationResultDto?>(null);
    }

    public Task<AiTiltInterventionResultDto?> AnalyzeTiltInterventionAsync(AiTiltInterventionRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<AiTiltInterventionResultDto?>(null);
    }
}