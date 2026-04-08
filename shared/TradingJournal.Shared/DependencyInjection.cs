using TradingJournal.Shared.Security;
using TradingJournal.Shared.Common;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace TradingJournal.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedModule(this IServiceCollection services)
    {
        services.AddSingleton<ICacheRepository, CacheRepository>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IUserContext, UserContext>();

        services.AddHybridCache();

        return services;
    }

    public static IServiceCollection AddUserAwareBehavior(this IServiceCollection services)
    {
        return services;
    }
}