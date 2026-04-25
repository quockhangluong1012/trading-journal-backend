using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Backtest.EventHandlers;
using TradingJournal.Modules.Backtest.Events;
using TradingJournal.Shared.Behaviors;
using TradingJournal.Shared.MediatR;

namespace TradingJournal.Modules.Backtest;

public static class DependencyInjection
{
    public static IServiceCollection AddBacktestModule(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment = false)
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

        // Database
        services.AddScoped<IBacktestDbContext, BacktestDbContext>();

        services.AddDbContext<BacktestDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("BacktestDatabase"));
        });

        // Core services
        services.AddScoped<IOrderMatchingEngine, OrderMatchingEngine>();
        services.AddScoped<IPlaybackEngine, PlaybackEngine>();
        services.AddScoped<ICandleAggregationService, CandleAggregationService>();

        // Market data provider (Twelve Data — supports Forex, Metals, Futures)
        services.Configure<TwelveDataOptions>(configuration.GetSection(TwelveDataOptions.SectionName));
        services.AddHttpClient<IMarketDataProvider, TwelveDataMarketDataProvider>();

        // Background services for data sync
        services.AddHostedService<DataSyncBackgroundService>();
        services.AddHostedService<CsvImportBackgroundService>();

        // Event handlers
        services.AddTransient<INotificationHandler<FetchHistoricalDataEvent>,
            FetchHistoricalDataEventHandler>();

        // SignalR
        services.AddSignalR();

        return services;
    }

    public static async Task<IApplicationBuilder> MigrateBacktestDatabase(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        try
        {
            BacktestDbContext dbContext = scope.ServiceProvider.GetRequiredService<BacktestDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("BacktestMigration");
            logger.LogError(ex, "Failed to migrate Backtest database.");
        }

        return app;
    }
}
