using Microsoft.Extensions.DependencyInjection;
using TradingJournal.Modules.Trades.Dto;
using TradingJournal.Modules.Trades.Events;
using TradingJournal.Modules.Trades.Services;

namespace TradingJournal.Modules.Trades.EventHandlers;

internal sealed class GenerateReviewSummaryEventHandler(
    IServiceScopeFactory serviceScopeFactory) : INotificationHandler<GenerateReviewSummaryEvent>
{
    public async Task Handle(GenerateReviewSummaryEvent notification, CancellationToken cancellationToken)
    {
        using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

        IOpenRouterAIService aiService = scope.ServiceProvider.GetRequiredService<IOpenRouterAIService>();
        ITradeDbContext context = scope.ServiceProvider.GetRequiredService<ITradeDbContext>();

        ReviewSummaryRequestDto aiRequest = new(
            notification.PeriodType,
            notification.PeriodStart,
            notification.PeriodEnd,
            notification.UserId);

        try
        {
            ReviewSummaryResultDto? aiResult = await aiService.GenerateReviewSummary(aiRequest, cancellationToken);

            if (aiResult is null)
            {
                await SetGeneratingFalse(context, notification, cancellationToken);
                return;
            }

            // Upsert the AI result to the review record
            TradingReview? existing = await context.TradingReviews
                .FirstOrDefaultAsync(r =>
                    r.CreatedBy == notification.UserId &&
                    r.PeriodType == notification.PeriodType &&
                    r.PeriodStart == notification.PeriodStart,
                    cancellationToken);

            if (existing is not null)
            {
                existing.AiSummary = aiResult.Summary;
                existing.AiStrengths = aiResult.StrengthsAnalysis;
                existing.AiWeaknesses = aiResult.WeaknessAnalysis;
                existing.AiActionItems = string.Join("|||", aiResult.ActionItems);
                existing.AiTechnicalInsights = aiResult.TechnicalInsights;
                existing.AiPsychologyAnalysis = aiResult.PsychologyAnalysis;
                existing.AiCriticalMistakesTechnical = string.Join("|||", aiResult.CriticalMistakes?.Technical ?? []);
                existing.AiCriticalMistakesPsychological = string.Join("|||", aiResult.CriticalMistakes?.Psychological ?? []);
                existing.AiWhatToImprove = string.Join("|||", aiResult.WhatToImprove ?? []);
                existing.AiSummaryGenerating = false;

                context.TradingReviews.Update(existing);
            }
            else
            {
                TradingReview review = new()
                {
                    Id = 0,
                    PeriodType = notification.PeriodType,
                    PeriodStart = notification.PeriodStart,
                    PeriodEnd = notification.PeriodEnd,
                    AiSummary = aiResult.Summary,
                    AiStrengths = aiResult.StrengthsAnalysis,
                    AiWeaknesses = aiResult.WeaknessAnalysis,
                    AiActionItems = string.Join("|||", aiResult.ActionItems),
                    AiTechnicalInsights = aiResult.TechnicalInsights,
                    AiPsychologyAnalysis = aiResult.PsychologyAnalysis,
                    AiCriticalMistakesTechnical = string.Join("|||", aiResult.CriticalMistakes?.Technical ?? []),
                    AiCriticalMistakesPsychological = string.Join("|||", aiResult.CriticalMistakes?.Psychological ?? []),
                    AiWhatToImprove = string.Join("|||", aiResult.WhatToImprove ?? []),
                    AiSummaryGenerating = false,
                };

                await context.TradingReviews.AddAsync(review, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // On failure, ensure we reset the generating flag
            await SetGeneratingFalse(context, notification, cancellationToken);
            throw;
        }
    }

    private static async Task SetGeneratingFalse(
        ITradeDbContext context,
        GenerateReviewSummaryEvent notification,
        CancellationToken cancellationToken)
    {
        TradingReview? review = await context.TradingReviews
            .FirstOrDefaultAsync(r =>
                r.CreatedBy == notification.UserId &&
                r.PeriodType == notification.PeriodType &&
                r.PeriodStart == notification.PeriodStart,
                cancellationToken);

        if (review is not null)
        {
            review.AiSummaryGenerating = false;
            context.TradingReviews.Update(review);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
