using MediatR;

namespace TradingJournal.Messaging.Shared.Abstractions;

public interface IIntegrationEvent : INotification
{
    Guid EventId { get; }
}