using TradingJournal.Shared.Security;
using TradingJournal.Shared.Common;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Infrastructure;
using TradingJournal.Shared.Repositories;
using TradingJournal.Shared.Idempotency;
using TradingJournal.Shared.Audit;
using Microsoft.Extensions.DependencyInjection;

namespace TradingJournal.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddSharedModule(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IUserContext, UserContext>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // HybridCache provides the underlying cache infrastructure.
        // ICacheRepository is a domain-level wrapper used by handlers across modules.
        services.AddHybridCache();
        services.AddSingleton<ICacheRepository, CacheRepository>();

        // Idempotency: prevent duplicate mutations from network retries
        services.AddSingleton<IIdempotencyStore, SqlIdempotencyStore>();
        services.AddHostedService<IdempotencyCleanupService>();

        // Audit trail: track all entity changes
        services.AddSingleton<IAuditLogStore, SqlAuditLogStore>();

        return services;
    }
}