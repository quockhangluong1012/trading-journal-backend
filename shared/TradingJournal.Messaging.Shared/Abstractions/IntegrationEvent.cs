namespace TradingJournal.Messaging.Shared.Abstractions;

public abstract record IntegrationEvent(Guid EventId) : IIntegrationEvent;