using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingJournal.Shared.Behaviors;
using TradingJournal.Shared.MediatR;

namespace TradingJournal.Shared.Extensions;

/// <summary>
/// Shared extension methods that eliminate duplicated DI boilerplate across all modules.
/// Each module should call AddModuleDefaults() instead of repeating MediatR, FluentValidation,
/// and LoggingBehavior registration.
/// </summary>
public static class ModuleExtensions
{
    /// <summary>
    /// Registers the standard module services: FluentValidation validators, MediatR handlers,
    /// ValidationBehavior, UserAwareBehavior, and (in dev) LoggingBehavior for the given assembly.
    /// </summary>
    public static IServiceCollection AddModuleDefaults(
        this IServiceCollection services,
        Assembly moduleAssembly,
        bool isDevelopment = false)
    {
        services.AddValidatorsFromAssembly(moduleAssembly);

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(moduleAssembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
            config.AddOpenBehavior(typeof(UserAwareBehavior<,>));

            // Performance logging runs in all environments — slow queries in production
            // are exactly when you need visibility. Log level is controlled by configuration.
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        return services;
    }

    /// <summary>
    /// Registers a module DbContext with SQL Server, using the standard connection string key
    /// and retry-on-failure policy.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        string? migrationsHistoryTable = null,
        string? migrationsSchema = null)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);

                if (migrationsHistoryTable is not null)
                {
                    sqlOptions.MigrationsHistoryTable(migrationsHistoryTable, migrationsSchema);
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Generic database migration method that replaces the duplicated MigrateXxxDatabase()
    /// pattern across all modules. Logs errors instead of crashing the application.
    /// </summary>
    public static async Task<IApplicationBuilder> MigrateModuleDatabase<TContext>(
        this IApplicationBuilder app,
        string moduleName)
        where TContext : DbContext
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        try
        {
            TContext dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger($"{moduleName}Migration");
            logger.LogError(ex, "Failed to migrate {ModuleName} database.", moduleName);
        }

        return app;
    }
}
