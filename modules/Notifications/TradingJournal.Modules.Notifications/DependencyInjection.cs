using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TradingJournal.Shared.Extensions;

namespace TradingJournal.Modules.Notifications;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services,
        IConfiguration configuration, bool isDevelopment = false)
    {
        services.AddModuleDefaults(Assembly.GetExecutingAssembly(), isDevelopment);

        string connectionString = configuration.GetConnectionString("TradeDatabase")!;
        services.AddModuleDbContext<NotificationDbContext>(connectionString);

        // Database
        services.AddScoped<INotificationDbContext, NotificationDbContext>();

        // Core services
        services.AddScoped<INotificationService, NotificationService>();

        // SignalR (idempotent — safe to call even if already registered by another module)
        services.AddSignalR();

        return services;
    }

    public static async Task MigrateNotificationDatabase(this WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        NotificationDbContext context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await context.Database.MigrateAsync();
    }
}
