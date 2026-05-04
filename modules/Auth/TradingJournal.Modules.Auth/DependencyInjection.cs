using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Auth;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        string connectionString = configuration.GetConnectionString("TradeDatabase")!;
        services.AddModuleDbContext<AuthDbContext>(connectionString);

        services.AddScoped<IAuthDbContext, AuthDbContext>();

        return services;
    }
}
