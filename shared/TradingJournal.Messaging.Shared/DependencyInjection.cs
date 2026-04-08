using Microsoft.Extensions.DependencyInjection;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Messaging.Shared.Events;

namespace TradingJournal.Messaging.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddInMemoryMessageQueue(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryMessageQueue>();
        services.AddScoped<IEventBus, EventBus>();
        services.AddHostedService<IntegrationEventProcessorJob>();
        
        return services;
    }
}