using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Modules.AiInsights.Events;

public record GenerateReviewSummaryEvent(
    Guid EventId,
    DateTime EventTime,
    ReviewPeriodType PeriodType,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int UserId,
    int RuleBreaks) : IntegrationEvent(EventId);
