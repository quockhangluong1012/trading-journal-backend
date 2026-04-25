namespace TradingJournal.Modules.Trades.Services;

public interface IPromptService
{
    public Task<string> GetTradingOrderSummary();

    public Task<string> GetReviewSummary();

    public Task<string> GetAiCoach();
}
