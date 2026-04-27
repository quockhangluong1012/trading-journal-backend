using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingJournal.Modules.AiInsights.Extensions;
using TradingJournal.Modules.AiInsights.Options;
using TradingJournal.Modules.AiInsights.Services;

namespace TradingJournal.Modules.AiInsights;

public static class DependencyInjection
{
    public static IServiceCollection AddAiInsightsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        services.AddDbContext<AiInsightsDbContext>(options =>
        {
            string connectionString = isDevelopment
                ? configuration.GetConnectionString("TradeDatabase")!
                : Environment.GetEnvironmentVariable("DEFAULT_CONNECTION")!;

            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsHistoryTable("__AiInsightsMigrationsHistory", "Trades");
            });
        });

        services.AddScoped<IAiInsightsDbContext>(sp =>
            sp.GetRequiredService<AiInsightsDbContext>());

        // AI services
        services.Configure<OpenRouterOptions>(configuration.GetSection(OpenRouterOptions.BindLocator));
        services.AddScoped<IPromptService, PromptService>();
        services.AddScoped<IImageHelper, ImageHelper>();

        services.AddHttpClient<IOpenRouterAIService, OpenRouterAiService>((sp, client) =>
        {
            OpenRouterOptions openRouterOptions = configuration
                .GetSection(OpenRouterOptions.BindLocator)
                .Get<OpenRouterOptions>()!;

            client.BaseAddress = new Uri(openRouterOptions.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        });

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
