using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Modules.Trades.Events;

public record GenerateReviewSummaryEvent(
    Guid EventId,
    DateTime EventTime,
    ReviewPeriodType PeriodType,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int UserId) : IntegrationEvent(EventId);
