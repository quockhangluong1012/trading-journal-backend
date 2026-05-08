namespace TradingJournal.Modules.AiInsights.Services;

public interface IPromptService
{
    public Task<string> GetTradingOrderSummary();

    public Task<string> GetReviewSummary();

    public Task<string> GetAiCoach();

    public Task<string> GetPreTradeValidation();

    public Task<string> GetChartScreenshotAnalysis();

    public Task<string> GetEmotionDetection();

    public Task<string> GetRiskAdvisor();

    public Task<string> GetWeeklyDigest();

    public Task<string> GetEconomicImpactPredictor();

    public Task<string> GetMorningBriefing();

    public Task<string> GetNaturalLanguageTradeSearch();

    public Task<string> GetTradePatternDiscovery();

    public Task<string> GetSuggestedLessons();

    public Task<string> GetPlaybookOptimization();

    public Task<string> GetTiltIntervention();

    public Task<string> GetTradingSetupGeneration();

    public Task<string> GetChecklistInterpretation();
}
