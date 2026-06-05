namespace TradingJournal.Messaging.Shared.Abstractions;

/// <summary>
/// Receives integration events that could not be handled after every retry attempt was
/// exhausted, so they are captured durably instead of being silently dropped. The default
/// implementation logs at Critical level; swap it for a database/queue-backed sink to make
/// failed events replayable.
/// </summary>
public interface IDeadLetterSink
{
    Task SendAsync(IIntegrationEvent integrationEvent, Exception exception, int attempts, CancellationToken cancellationToken);
}
