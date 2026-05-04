using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TradingJournal.Modules.Psychology.Helpers;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Psychology;

public static class DependencyInjection
{
    public static IServiceCollection AddPsychologyModule(this IServiceCollection services, IConfiguration configuration,
        bool isDevelopment = false)
    {
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        string connectionString = configuration.GetConnectionString("TradeDatabase")!;
        services.AddModuleDbContext<PsychologyDbContext>(connectionString);

        services.AddScoped<IPsychologyDbContext, PsychologyDbContext>();
        services.AddScoped<IEmotionTagProvider, EmotionTagProvider>();
        services.AddScoped<IPsychologyProvider, PsychologyProvider>();
        services.AddScoped<ITiltDetectionService, TiltDetectionService>();
        services.AddScoped<IStreakTrackingService, StreakTrackingService>();
        services.AddScoped<IKarmaService, KarmaService>();

        return services;
    }

    public static async Task MigratePsychologyDatabase(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        PsychologyDbContext context = scope.ServiceProvider.GetRequiredService<PsychologyDbContext>();
        await context.Database.MigrateAsync();
    }
}