using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingJournal.Modules.AiInsights.Extensions;
using TradingJournal.Modules.AiInsights.Options;
using TradingJournal.Modules.AiInsights.Services;
using TradingJournal.Shared.Extensions;
using Microsoft.EntityFrameworkCore;

namespace TradingJournal.Modules.AiInsights;

public static class DependencyInjection
{
    public static IServiceCollection AddAiInsightsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        // Database — uses standard connection string resolution like all other modules
        string connectionString = configuration.GetConnectionString("TradeDatabase")!;
        services.AddModuleDbContext<AiInsightsDbContext>(connectionString,
            migrationsHistoryTable: "__AiInsightsMigrationsHistory",
            migrationsSchema: "Trades");

        services.AddScoped<IAiInsightsDbContext>(sp =>
            sp.GetRequiredService<AiInsightsDbContext>());

        // AI services
        services.AddOptions<OpenRouterOptions>()
            .Bind(configuration.GetSection(OpenRouterOptions.BindLocator))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IPromptService, PromptService>();
        services.AddScoped<IImageHelper, ImageHelper>();

        services.AddHttpClient<IOpenRouterAIService, OpenRouterAiService>((sp, client) =>
        {
            OpenRouterOptions openRouterOptions = configuration
                .GetSection(OpenRouterOptions.BindLocator)
                .Get<OpenRouterOptions>()!;

            client.BaseAddress = new Uri(openRouterOptions.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        })
        .AddStandardResilienceHandler();

        // MediatR for this assembly (features + event handlers)
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }

    public static async Task MigrateAiInsightsDatabase(this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        AiInsightsDbContext context = scope.ServiceProvider.GetRequiredService<AiInsightsDbContext>();
        await context.Database.MigrateAsync();
    }
}
