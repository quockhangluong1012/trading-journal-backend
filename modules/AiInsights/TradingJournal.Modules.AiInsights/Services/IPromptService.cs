namespace TradingJournal.Modules.AiInsights.Services;

public interface IPromptService
{
    public Task<string> GetTradingOrderSummary();

    public Task<string> GetReviewSummary();

    public Task<string> GetAiCoach();
}
