using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TradingJournal.Shared.Behaviors;

namespace TradingJournal.Modules.Auth;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
            config.AddOpenBehavior(typeof(Shared.MediatR.UserAwareBehavior<,>));

            if (isDevelopment)
            {
                config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            }
        });

        services.AddDbContext<AuthDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("TradeDatabase"));
        });

        services.AddScoped<IAuthDbContext, AuthDbContext>();

        return services;
    }
}
