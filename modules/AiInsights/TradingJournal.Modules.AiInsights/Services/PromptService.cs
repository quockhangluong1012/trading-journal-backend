using System.Reflection;

namespace TradingJournal.Modules.AiInsights.Services
{
    public sealed class PromptService(ICacheRepository cacheRepository) : IPromptService
    {
        public Task<string> GetTradingOrderSummary() => GetPrompt("TradingOrderSummary");

        public Task<string> GetReviewSummary() => GetPrompt("ReviewSummary");

        public Task<string> GetAiCoach() => GetPrompt("AiCoach");

        public Task<string> GetPreTradeValidation() => GetPrompt("PreTradeValidation");

        public Task<string> GetChartScreenshotAnalysis() => GetPrompt("ChartScreenshotAnalysis");

        public Task<string> GetEmotionDetection() => GetPrompt("EmotionDetection");

        public Task<string> GetRiskAdvisor() => GetPrompt("RiskAdvisor");

        public Task<string> GetWeeklyDigest() => GetPrompt("WeeklyDigest");

        public Task<string> GetEconomicImpactPredictor() => GetPrompt("EconomicImpactPredictor");

        public Task<string> GetMorningBriefing() => GetPrompt("MorningBriefing");

        public Task<string> GetNaturalLanguageTradeSearch() => GetPrompt("NaturalLanguageTradeSearch");

        public Task<string> GetTradePatternDiscovery() => GetPrompt("TradePatternDiscovery");

        public Task<string> GetSuggestedLessons() => GetPrompt("SuggestedLessons");

        public Task<string> GetPlaybookOptimization() => GetPrompt("PlaybookOptimization");

        public Task<string> GetTiltIntervention() => GetPrompt("TiltIntervention");

        public Task<string> GetTradingSetupGeneration() => GetPrompt("TradingSetupGeneration");

        public Task<string> GetChecklistInterpretation() => GetPrompt("ChecklistInterpretation");

        private async Task<string> GetPrompt(string promptName)
        {
            string prompt = await cacheRepository.GetOrCreateAsync<string>(
               key: promptName,
                  handle: async (cancellationToken) =>
                   {
                       Assembly assembly = typeof(PromptService).Assembly;

                       string resourceName = $"TradingJournal.Modules.AiInsights.Prompts.{promptName}.md";

                       using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new FileNotFoundException($"Prompt resource not found: {promptName}");

                       using var reader = new StreamReader(stream);
                       return await reader.ReadToEndAsync(cancellationToken);
                   },
           expiration:TimeSpan.FromHours(24)) ?? string.Empty;

            return prompt;
        }
    }
}
