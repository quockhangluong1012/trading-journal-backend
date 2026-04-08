using System.Reflection;

namespace TradingJournal.Modules.Trades.Services
{
    public sealed class PromptService(ICacheRepository cacheRepository) : IPromptService
    {
        public Task<string> GetTradingOrderSummary() => GetPrompt("TradingOrderSummary");

        public Task<string> GetReviewSummary() => GetPrompt("ReviewSummary");

        private async Task<string> GetPrompt(string promptName)
        {
            string prompt = await cacheRepository.GetOrCreateAsync<string>(
               key: promptName,
                  handle: async (cancellationToken) =>
                   {
                       Assembly assembly = typeof(PromptService).Assembly;

                       string resourceName = $"TradingJournal.Modules.Trades.Prompts.{promptName}.md";

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
