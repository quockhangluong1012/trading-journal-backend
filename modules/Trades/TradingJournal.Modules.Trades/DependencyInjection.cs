using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TradingJournal.Modules.Trades.EventHandlers;
using TradingJournal.Modules.Trades.Events;
using TradingJournal.Modules.Trades.Extensions;
using TradingJournal.Modules.Trades.Options;
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

        services.AddOpenRouterAI(configuration);

        services.AddEventHandlers();

        services.AddHelpers();

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
        catch (Exception)
        {
            // Log exception if needed
        }

        return app;
    }

    private static IServiceCollection AddOpenRouterAI(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IOpenRouterAIService, OpenRouterAIService>();
        services.AddTransient<IPromptService, PromptService>();

        services.Configure<OpenRouterOptions>(configuration.GetSection(OpenRouterOptions.BindLocator));

        return services;
    }

    private static IServiceCollection AddEventHandlers(this IServiceCollection services)
    {
        services.AddTransient<INotificationHandler<SummarizeTradingOrderEvent>,
            SummarizeTradingOrderEventHandler>();

        services.AddTransient<INotificationHandler<GenerateReviewSummaryEvent>,
            GenerateReviewSummaryEventHandler>();

        return services;
    }

    private static IServiceCollection AddHelpers(this IServiceCollection services)
    {
        services.AddHttpClient<IImageHelper, ImageHelper>();
        return services;
    }
}
