using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Events;

internal sealed class EventBus(InMemoryMessageQueue queue) : IEventBus
{
    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellation) where T : class, IIntegrationEvent
    {
        await queue.Writer.WriteAsync(integrationEvent, cancellation);
    }
}