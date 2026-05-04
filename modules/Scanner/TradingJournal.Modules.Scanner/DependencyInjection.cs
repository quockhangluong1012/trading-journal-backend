using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingJournal.Modules.Scanner.Services;
using TradingJournal.Modules.Scanner.Services.EconomicCalendar;
using TradingJournal.Modules.Scanner.Services.ICTAnalysis;
using TradingJournal.Modules.Scanner.Services.LiveData;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Scanner;

public static class DependencyInjection
{
    public static IServiceCollection AddScannerModule(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment = false)
    {
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        // Database
        string connectionString = configuration.GetConnectionString("TradeDatabase")!;
        services.AddModuleDbContext<ScannerDbContext>(connectionString);
        services.AddScoped<IScannerDbContext, ScannerDbContext>();

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

        // Live market data provider (Yahoo Finance — free, supports all symbols including NASDAQ indices)
        services.AddHttpClient<ILiveMarketDataProvider, YahooFinanceLiveProvider>()
            .AddStandardResilienceHandler();

        // Core services
        services.AddSingleton<MultiTimeframeAnalyzer>();
        services.AddScoped<IScannerEngine, ScannerEngine>();

        // Background scanner service
        services.AddHostedService<ScannerBackgroundService>();

        // Economic Calendar (Forex Factory feed — free, no API key needed)
        services.AddHttpClient<IEconomicCalendarProvider, EconomicCalendarProvider>()
            .AddStandardResilienceHandler();
        services.AddHostedService<EconomicCalendarBackgroundService>();

        // SignalR (idempotent)
        services.AddSignalR();

        return services;
    }

    public static async Task MigrateScannerDatabase(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        ScannerDbContext context = scope.ServiceProvider.GetRequiredService<ScannerDbContext>();
        await context.Database.MigrateAsync();
    }
}
