using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Events;

namespace TradingJournal.Messaging.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddInMemoryMessageQueue(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryMessageQueue>();
        services.AddScoped<IEventBus, EventBus>();
        services.TryAddSingleton<IDeadLetterSink, LoggingDeadLetterSink>();
        services.AddHostedService<IntegrationEventProcessorJob>();
        
        return services;
    }
}