using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TradingJournal.Modules.RiskManagement.Services;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.RiskManagement;

public static class DependencyInjection
{
    public static IServiceCollection AddRiskManagementModule(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment = false)
    {
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        string connectionString = configuration.GetConnectionString("TradeDatabase")!;
        services.AddModuleDbContext<RiskDbContext>(connectionString);

        services.AddScoped<IRiskDbContext, RiskDbContext>();
        services.AddScoped<IRiskContextProvider, RiskContextProvider>();

        return services;
    }

    public static async Task MigrateRiskManagementDatabase(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        RiskDbContext context = scope.ServiceProvider.GetRequiredService<RiskDbContext>();
        await context.Database.MigrateAsync();
    }
}
