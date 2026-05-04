using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Setups;

public static class DependencyInjection
{
    public static IServiceCollection AddTradingSetupModule(this IServiceCollection services, IConfiguration configuration,
        bool isDevelopment = false)
    {
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        string connectionString = configuration.GetConnectionString("TradeDatabase")!;
        services.AddModuleDbContext<SetupDbContext>(connectionString);

        services.AddScoped<ISetupDbContext, SetupDbContext>();
        services.AddScoped<ISetupProvider, SetupProvider>();

        return services;
    }

    public static async Task MigrateSetupDatabase(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        SetupDbContext context = scope.ServiceProvider.GetRequiredService<SetupDbContext>();
        await context.Database.MigrateAsync();
    }
}
