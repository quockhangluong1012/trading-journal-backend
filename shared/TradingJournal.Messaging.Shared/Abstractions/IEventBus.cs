namespace TradingJournal.Messaging.Shared.Abstractions;

public interface IEventBus
{
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellation) 
        where T : class, IIntegrationEvent;
}