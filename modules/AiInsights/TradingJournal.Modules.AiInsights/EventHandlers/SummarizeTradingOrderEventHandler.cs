using Microsoft.Extensions.DependencyInjection;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Services;
using TradingJournal.Messaging.Shared.Events;

namespace TradingJournal.Modules.AiInsights.EventHandlers;

internal sealed class SummarizeTradingOrderEventHandler(
    IServiceScopeFactory serviceScopeFactory) : INotificationHandler<SummarizeTradingOrderEvent>
{
    public async Task Handle(SummarizeTradingOrderEvent notification, CancellationToken cancellationToken)
    {
        using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

        IOpenRouterAIService aiService = scope.ServiceProvider.GetRequiredService<IOpenRouterAIService>();
        IAiInsightsDbContext context = scope.ServiceProvider.GetRequiredService<IAiInsightsDbContext>();
        IAiTradeDataProvider tradeDataProvider = scope.ServiceProvider.GetRequiredService<IAiTradeDataProvider>();

        TradeAnalysisResultDto? result = await aiService.GenerateTradingOrderSummary(notification.TradeHistoryId, cancellationToken)
            ?? throw new AggregateException($"Failed to generate trading order summary for TradeHistoryId: {notification.TradeHistoryId}");

        TradingSummary tradingSummary = new()
        {
            Id = 0,
            TradeId = notification.TradeHistoryId,
            ExecutiveSummary = result?.ExecutiveSummary ?? string.Empty,
            TechnicalInsights = result?.TechnicalInsights ?? string.Empty,
            PsychologyAnalysis = result?.PsychologyAnalysis ?? string.Empty,
            CriticalMistakes = new CriticalMistakes()
            {
                Psychological = result?.CriticalMistakes?.Psychological ?? [],
                Technical = result?.CriticalMistakes?.Technical ?? [],
            },
        };

        await context.TradingSummaries.AddAsync(tradingSummary, cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Update TradeHistory via shared interface (cross-module)
        await tradeDataProvider.UpdateTradeSummaryIdAsync(notification.TradeHistoryId, tradingSummary.Id, cancellationToken);
    }
}
