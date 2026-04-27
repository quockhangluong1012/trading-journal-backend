using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using TradingJournal.Modules.Trades.Features.V1.Review;
using TradingJournal.Modules.Trades.Services;
using TradingJournal.Shared.Behaviors;
using TradingJournal.Shared.MediatR;

namespace TradingJournal.Modules.Trades;

public static class DependencyInjection
{
    public static IServiceCollection AddTradeModule(this IServiceCollection services, IConfiguration configuration,
        bool isDevelopment = false)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
            config.AddOpenBehavior(typeof(UserAwareBehavior<,>));

            if (isDevelopment)
            {
                config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            }
        });

        services.AddScoped<ITradeDbContext, TradeDbContext>();

        services.AddDbContext<TradeDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("TradeDatabase"));
        });

        services.AddScoped<ITradeProvider, TradeProvider>();

        // ReviewSnapshotBuilder stays in Trades (it needs ITradeDbContext)
        services.AddScoped<IReviewSnapshotBuilder, ReviewSnapshotBuilder>();

        // Cross-module data provider for AiInsights module
        services.AddScoped<IAiTradeDataProvider, AiTradeDataProvider>();

        return services;
    }

    public static async Task<IApplicationBuilder> MigrateTradingDatabase(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        try
        {
            TradeDbContext dbContext = scope.ServiceProvider.GetRequiredService<TradeDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("TradesMigration");
            logger.LogError(ex, "Failed to migrate Trades database.");
        }

        return app;
    }
}
