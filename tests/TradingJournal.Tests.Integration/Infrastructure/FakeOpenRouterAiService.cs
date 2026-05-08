using System.Threading;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Tests.Integration.Infrastructure;

public sealed class FakeOpenRouterAiService : IOpenRouterAIService
{
    private int _analyzeChartScreenshotCalls;
    private int _generateTradingSetupCalls;

    public int AnalyzeChartScreenshotCalls => _analyzeChartScreenshotCalls;

    public int GenerateTradingSetupCalls => _generateTradingSetupCalls;

    public void Reset()
    {
        Interlocked.Exchange(ref _analyzeChartScreenshotCalls, 0);
        Interlocked.Exchange(ref _generateTradingSetupCalls, 0);
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

    public Task<TradingSetupGenerationResultDto?> GenerateTradingSetupAsync(TradingSetupGenerationRequestDto request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _generateTradingSetupCalls);

        return Task.FromResult<TradingSetupGenerationResultDto?>(new TradingSetupGenerationResultDto
        {
            Summary = "Fake AI setup preview for integration testing.",
            Name = "AI Venom Model",
            Description = "Liquidity sweep into displacement and continuation.",
            Confidence = 0.86m,
            Assumptions = ["Assumes London open liquidity is the primary draw."],
            Warnings = ["Confirm displacement before entry."],
            Nodes =
            [
                new TradingSetupGenerationNodeDto
                {
                    Id = "start-1",
                    Kind = "start",
                    X = 0,
                    Y = 0,
                    Title = "Sweep liquidity",
                    Notes = "Wait for the Venom sweep during London open.",
                },
                new TradingSetupGenerationNodeDto
                {
                    Id = "step-1",
                    Kind = "step",
                    X = 220,
                    Y = 0,
                    Title = "Confirm displacement",
                    Notes = "Require displacement through the reclaimed range.",
                },
                new TradingSetupGenerationNodeDto
                {
                    Id = "end-1",
                    Kind = "end",
                    X = 440,
                    Y = 0,
                    Title = "Target continuation",
                    Notes = "Manage partials into the continuation leg.",
                },
            ],
            Edges =
            [
                new TradingSetupGenerationEdgeDto
                {
                    Id = "edge-1",
                    Source = "start-1",
                    Target = "step-1",
                    Label = "Sweep complete",
                },
                new TradingSetupGenerationEdgeDto
                {
                    Id = "edge-2",
                    Source = "step-1",
                    Target = "end-1",
                    Label = "Displacement confirmed",
                },
            ],
        });
    }

    public Task<PreTradeChecklistInterpretationResultDto?> InterpretPreTradeChecklistAsync(PreTradeChecklistInterpretationRequestDto request, CancellationToken cancellationToken)
    {
        return Task.FromResult<PreTradeChecklistInterpretationResultDto?>(null);
    }
}