using Serilog;
using Serilog.Events;

namespace TradingJournal.ApiGateWay.Extensions;

/// <summary>
/// Serilog bootstrap and request-logging pipeline helpers.
/// </summary>
internal static class SerilogExtensions
{
    /// <summary>
    /// Configures Serilog as the logging provider using settings from appsettings.json.
    /// Call this on the <see cref="WebApplicationBuilder"/> before building the app.
    /// </summary>
    public static WebApplicationBuilder AddSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProperty("Application", "TradingJournal");
        });

        return builder;
    }

    /// <summary>
    /// Adds Serilog HTTP request logging with enriched diagnostic context
    /// (UserId, ClientIp, and response body size).
    /// </summary>
    public static IApplicationBuilder UseSerilogHttpLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            // Customize the log message template
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            // Attach extra properties to every request log event
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("ClientIp",
                    StartupConfigurationExtensions.GetClientIpAddress(httpContext));

                diagnosticContext.Set("UserAgent",
                    httpContext.Request.Headers.UserAgent.ToString());

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    string userId = httpContext.User.FindFirst(
                        System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

                    if (!string.IsNullOrEmpty(userId))
                    {
                        diagnosticContext.Set("UserId", userId);
                    }
                }

                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("ContentType",
                    httpContext.Response.ContentType ?? "unknown");
            };

            // Downgrade health-check and swagger noise to Verbose
            options.GetLevel = (httpContext, elapsed, ex) =>
            {
                if (ex is not null)
                    return LogEventLevel.Error;

                if (httpContext.Response.StatusCode >= 500)
                    return LogEventLevel.Error;

                string path = httpContext.Request.Path.Value ?? string.Empty;

                if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase))
                {
                    return LogEventLevel.Verbose;
                }

                return LogEventLevel.Information;
            };
        });
    }
}
