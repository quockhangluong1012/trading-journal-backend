namespace TradingJournal.ApiGateWay.Extensions;

public static class SecurityHeadersExtensions
{
    private const string DefaultContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'none'";
    private const string DocumentationContentSecurityPolicy = "default-src 'self'; img-src 'self' data: https:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; connect-src 'self'; font-src 'self' data: https:; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IHostEnvironment environment)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.TryAdd("X-Content-Type-Options", "nosniff");
                headers.TryAdd("X-Frame-Options", "DENY");
                headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
                headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
                headers.TryAdd("Content-Security-Policy", GetContentSecurityPolicy(context.Request.Path));

                if (!environment.IsDevelopment())
                {
                    headers.TryAdd("Cross-Origin-Opener-Policy", "same-origin");
                }

                return Task.CompletedTask;
            });

            await next();
        });
    }

    private static string GetContentSecurityPolicy(PathString path)
    {
        return path.StartsWithSegments("/scalar") ||
               path.StartsWithSegments("/openapi") ||
               path.StartsWithSegments("/swagger")
            ? DocumentationContentSecurityPolicy
            : DefaultContentSecurityPolicy;
    }
}