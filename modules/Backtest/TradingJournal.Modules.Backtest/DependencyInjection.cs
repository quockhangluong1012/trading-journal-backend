using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Backtest.EventHandlers;
using TradingJournal.Modules.Backtest.Events;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Backtest;

public static class DependencyInjection
{
    public static IServiceCollection AddBacktestModule(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment = false)
    {
        // Route through the shared helpers so Backtest gets the same validators, MediatR handlers,
        // and pipeline behaviors (Validation/UserAware/Logging) as every other module.
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        // Database (Backtest uses a separate database — the BacktestDatabase connection string).
        // AddModuleDbContext applies the shared EnableRetryOnFailure policy.
        services.AddScoped<IBacktestDbContext, BacktestDbContext>();
        services.AddModuleDbContext<BacktestDbContext>(configuration.GetConnectionString("BacktestDatabase")!);

        // Core services
        services.AddScoped<IOrderMatchingEngine, OrderMatchingEngine>();
        services.AddScoped<IPlaybackEngine, PlaybackEngine>();
        services.AddScoped<ICandleAggregationService, CandleAggregationService>();

        // Serializes per-session balance mutations across playback, manual close, and finish.
        // Singleton: the in-memory gate must be shared by every request/loop touching a session.
        services.AddSingleton<IBacktestSessionLock, BacktestSessionLock>();

        // Market data provider (Yahoo Finance — free, supports all symbols including NASDAQ indices).
        // The standard resilience handler adds retry, circuit breaker, and per-attempt/total timeouts —
        // important because this client is driven by background sync jobs against an external API with
        // no built-in timeout (default HttpClient would otherwise hang for 100s on a stalled response).
        services.AddHttpClient<IMarketDataProvider, YahooFinanceMarketDataProvider>()
            .AddStandardResilienceHandler();

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
