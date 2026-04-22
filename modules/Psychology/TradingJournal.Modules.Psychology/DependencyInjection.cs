using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using TradingJournal.Modules.Psychology.Helpers;
using TradingJournal.Shared.Behaviors;
using TradingJournal.Shared.MediatR;
namespace TradingJournal.Modules.Psychology;

public static class DependencyInjection
{
    public static IServiceCollection AddPsychologyModule(this IServiceCollection services, IConfiguration configuration,
        bool isDevelopment = false)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

            config.AddOpenBehavior(typeof(UserAwareBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));

            if (isDevelopment)
            {
                config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            }
        });

        services.AddScoped<IPsychologyDbContext, PsychologyDbContext>();
        services.AddScoped<IEmotionTagProvider, EmotionTagProvider>();
        services.AddScoped<IPsychologyProvider, PsychologyProvider>();

        services.AddDbContext<PsychologyDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("TradeDatabase"));
        });

        return services;
    }

    public static async Task<IApplicationBuilder> MigratePsychologyDatabase(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        try
        {
            PsychologyDbContext dbContext = scope.ServiceProvider.GetRequiredService<PsychologyDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("PsychologyMigration");
            logger.LogError(ex, "Failed to migrate Psychology database.");
        }

        return app;
    }
}