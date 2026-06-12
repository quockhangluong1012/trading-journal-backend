using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TradingJournal.Modules.Goals;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddGoalsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        string connectionString = configuration.GetConnectionString("TradeDatabase")!;
        services.AddModuleDbContext<GoalDbContext>(connectionString);
        services.AddScoped<IGoalDbContext, GoalDbContext>();

        return services;
    }

    public static async Task MigrateGoalsDatabase(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        GoalDbContext context = scope.ServiceProvider.GetRequiredService<GoalDbContext>();
        await context.Database.MigrateAsync();
    }
}
