using System.Reflection;

namespace TradingJournal.Modules.AiInsights.Services
{
    public sealed class PromptService(ICacheRepository cacheRepository) : IPromptService
    {
        public Task<string> GetTradingOrderSummary() => GetPrompt("TradingOrderSummary");

        public Task<string> GetReviewSummary() => GetPrompt("ReviewSummary");

        public Task<string> GetAiCoach() => GetPrompt("AiCoach");

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
