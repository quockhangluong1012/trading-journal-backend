using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Modules.AiInsights.Events;

public record GenerateReviewSummaryEvent(
    Guid EventId,
    DateTimeOffset EventTime,
    ReviewPeriodType PeriodType,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    int UserId) : IntegrationEvent(EventId);
