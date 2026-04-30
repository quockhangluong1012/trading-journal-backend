using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using TradingJournal.Shared.Behaviors;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.MediatR;

namespace TradingJournal.Modules.Setups;

public static class DependencyInjection
{
    public static IServiceCollection AddTradingSetupModule(this IServiceCollection services, IConfiguration configuration,
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

        services.AddScoped<ISetupDbContext, SetupDbContext>();
        services.AddScoped<ISetupProvider, SetupProvider>();

        services.AddDbContext<SetupDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("TradeDatabase"));
        });

        return services;
    }

    public static async Task<IApplicationBuilder> MigrateSetupDatabase(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        try
        {
            SetupDbContext dbContext = scope.ServiceProvider.GetRequiredService<SetupDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("TradingSetupMigration");
            logger.LogError(ex, "Failed to migrate TradingSetup database.");
        }

        return app;
    }
}
