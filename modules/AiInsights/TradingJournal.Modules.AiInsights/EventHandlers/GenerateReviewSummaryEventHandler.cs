using Microsoft.Extensions.DependencyInjection;
using TradingJournal.Modules.AiInsights.Dto;
using TradingJournal.Modules.AiInsights.Events;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights.EventHandlers;

internal sealed class GenerateReviewSummaryEventHandler(
    IServiceScopeFactory serviceScopeFactory) : INotificationHandler<GenerateReviewSummaryEvent>
{
    public async Task Handle(GenerateReviewSummaryEvent notification, CancellationToken cancellationToken)
    {
        using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

        IOpenRouterAIService aiService = scope.ServiceProvider.GetRequiredService<IOpenRouterAIService>();
        IAiInsightsDbContext context = scope.ServiceProvider.GetRequiredService<IAiInsightsDbContext>();

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
                existing.RuleBreaks = notification.RuleBreaks;
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
                    RuleBreaks = notification.RuleBreaks,
                    AiSummaryGenerating = false,
                };

                await context.TradingReviews.AddAsync(review, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            await SetGeneratingFalse(context, notification, cancellationToken);
            throw;
        }
    }

    private static async Task SetGeneratingFalse(
        IAiInsightsDbContext context,
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
