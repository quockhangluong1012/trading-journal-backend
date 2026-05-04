using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Analytics;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsModule(this IServiceCollection services,
        bool isDevelopment = false)
    {
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        return services;
    }
}
