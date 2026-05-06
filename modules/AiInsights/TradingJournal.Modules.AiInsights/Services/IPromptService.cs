namespace TradingJournal.Modules.AiInsights.Services;

public interface IPromptService
{
    public Task<string> GetTradingOrderSummary();

    public Task<string> GetReviewSummary();

    public Task<string> GetAiCoach();

    public Task<string> GetPreTradeValidation();

    public Task<string> GetEmotionDetection();

    public Task<string> GetMorningBriefing();
}
