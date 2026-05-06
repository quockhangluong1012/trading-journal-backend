using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TradingJournal.Modules.Trades.Services;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Trades;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeModule(this IServiceCollection services, IConfiguration configuration,
        bool isDevelopment = false)
    {
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        string connectionString = configuration.GetConnectionString("TradeDatabase")!;
        services.AddModuleDbContext<TradeDbContext>(connectionString);

        services.AddScoped<ITradeDbContext, TradeDbContext>();
        services.AddScoped<ITradeProvider, TradeProvider>();
        services.AddScoped<IZoneProvider, ZoneProvider>();
        services.AddScoped<ITechnicalAnalysisTagProvider, TechnicalAnalysisTagProvider>();

        // ReviewSnapshotBuilder stays in Trades (it needs ITradeDbContext)
        services.AddScoped<IReviewSnapshotBuilder, ReviewSnapshotBuilder>();

        // Cross-module data provider for AiInsights module
        services.AddScoped<IAiTradeDataProvider, AiTradeDataProvider>();

        // Extracted services from fat handlers
        services.AddScoped<IScreenshotService, ScreenshotService>();
        services.AddScoped<IDisciplineEvaluator, DisciplineEvaluator>();

        return services;
    }

    public static async Task MigrateTradingDatabase(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        TradeDbContext context = scope.ServiceProvider.GetRequiredService<TradeDbContext>();
        await context.Database.MigrateAsync();
    }
}
