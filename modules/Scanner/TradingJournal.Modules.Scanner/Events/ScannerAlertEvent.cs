using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Modules.Scanner.Events;

/// <summary>
/// Integration event published when the scanner detects a new ICT pattern.
/// Consumed by the Notification module's ScannerAlertNotificationHandler.
/// </summary>
public sealed record ScannerAlertEvent(
    Guid EventId,
    int UserId,
    string Symbol,
    string PatternType,
    string Timeframe,
    decimal Price,
    string Description,
    int ConfluenceScore) : IntegrationEvent(EventId);
