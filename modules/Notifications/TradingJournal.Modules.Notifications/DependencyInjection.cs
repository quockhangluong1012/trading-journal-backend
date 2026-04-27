using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingJournal.Shared.Behaviors;
using TradingJournal.Shared.MediatR;

namespace TradingJournal.Modules.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services,
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
        services.AddScoped<INotificationDbContext, NotificationDbContext>();

        services.AddDbContext<NotificationDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("NotificationDatabase"));
        });

        // Core services
        services.AddScoped<INotificationService, NotificationService>();

        // SignalR (idempotent — already registered by Backtest module, but safe to call again)
        services.AddSignalR();

        return services;
    }

    public static async Task<IApplicationBuilder> MigrateNotificationDatabase(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        try
        {
            NotificationDbContext dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("NotificationMigration");
            logger.LogError(ex, "Failed to migrate Notification database.");
        }

        return app;
    }
}
