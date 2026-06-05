using Microsoft.Extensions.Logging;
using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Events;

/// <summary>
/// Default <see cref="IDeadLetterSink"/> that records the failed event at Critical level with its
/// full payload. This guarantees a durable trace (via the configured Serilog sinks) so an event
/// that exhausts its retries is never silently lost. Replace with a database/queue-backed sink
/// when replayable, at-least-once delivery is required.
/// </summary>
internal sealed class LoggingDeadLetterSink(ILogger<LoggingDeadLetterSink> logger) : IDeadLetterSink
{
    public Task SendAsync(IIntegrationEvent integrationEvent, Exception exception, int attempts, CancellationToken cancellationToken)
    {
        logger.LogCritical(exception,
            "Dead-lettered integration event {IntegrationEventType} with EventId {IntegrationEventId} after {Attempts} failed attempt(s). Payload: {@IntegrationEvent}",
            integrationEvent.GetType().Name, integrationEvent.EventId, attempts, integrationEvent);

        return Task.CompletedTask;
    }
}
