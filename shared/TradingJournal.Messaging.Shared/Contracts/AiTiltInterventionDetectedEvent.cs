using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Contracts;

public sealed record AiTiltInterventionDetectedEvent(
    Guid EventId,
    int UserId,
    int TiltScore,
    string TiltLevel,
    string RiskLevel,
    string TiltType,
    string Title,
    string Message,
    IReadOnlyList<string> ActionItems) : IntegrationEvent(EventId);