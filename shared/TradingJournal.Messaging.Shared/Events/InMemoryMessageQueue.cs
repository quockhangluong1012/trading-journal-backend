using System.Threading.Channels;
using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Messaging.Shared.Events;

internal sealed class InMemoryMessageQueue
{
    private readonly Channel<IIntegrationEvent> _channel = Channel.CreateUnbounded<IIntegrationEvent>();
    
    public ChannelReader<IIntegrationEvent> Reader => _channel.Reader;
    
    public ChannelWriter<IIntegrationEvent> Writer => _channel.Writer;
}