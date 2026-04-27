using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Scanner.Services;
using TradingJournal.Modules.Scanner.Services.ICTAnalysis;
using TradingJournal.Shared.Behaviors;
using TradingJournal.Shared.MediatR;

namespace TradingJournal.Modules.Scanner;

public static class DependencyInjection
{
    public static IServiceCollection AddScannerModule(this IServiceCollection services,
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
        services.AddScoped<IScannerDbContext, ScannerDbContext>();

        services.AddDbContext<ScannerDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("ScannerDatabase"));
        });

        // ICT Pattern Detectors
        services.AddSingleton<IIctDetector, FvgDetector>();
        services.AddSingleton<IIctDetector, OrderBlockDetector>();
        services.AddSingleton<IIctDetector, BreakerBlockDetector>();
        services.AddSingleton<IIctDetector, LiquidityDetector>();
        services.AddSingleton<IIctDetector, LiquiditySweepDetector>();
        services.AddSingleton<IIctDetector, InversionFvgDetector>();
        services.AddSingleton<IIctDetector, UnicornModelDetector>();
        services.AddSingleton<IIctDetector, VenomModelDetector>();
        services.AddSingleton<IIctDetector, MitigationBlockDetector>();
        services.AddSingleton<IIctDetector, MarketStructureShiftDetector>();
        services.AddSingleton<IIctDetector, ChangeOfCharacterDetector>();
        services.AddSingleton<IIctDetector, DisplacementDetector>();
        services.AddSingleton<IIctDetector, OptimalTradeEntryDetector>();
        services.AddSingleton<IIctDetector, JudasSwingDetector>();
        services.AddSingleton<IIctDetector, BalancedPriceRangeDetector>();
        services.AddSingleton<IIctDetector, CisdDetector>();

        // Multi-asset detector (SMT Divergence)
        services.AddSingleton<IMultiAssetDetector, SmtDivergenceDetector>();

        // Core services
        services.AddSingleton<MultiTimeframeAnalyzer>();
        services.AddScoped<IScannerEngine, ScannerEngine>();

        // Background scanner service
        services.AddHostedService<ScannerBackgroundService>();

        // SignalR (idempotent)
        services.AddSignalR();

        return services;
    }

    public static async Task<IApplicationBuilder> MigrateScannerDatabase(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        try
        {
            ScannerDbContext dbContext = scope.ServiceProvider.GetRequiredService<ScannerDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("ScannerMigration");
            logger.LogError(ex, "Failed to migrate Scanner database.");
        }

        return app;
    }
}
