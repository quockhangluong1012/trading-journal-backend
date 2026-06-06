using Serilog.Context;

namespace TradingJournal.ApiGateway.Extensions;

/// <summary>
/// Correlation-ID middleware. Assigns (or honours an inbound <c>X-Correlation-ID</c> header) a
/// correlation id per request, echoes it back on the response, and pushes it onto Serilog's
/// <see cref="LogContext"/> so every log written during the request — across the HTTP pipeline and
/// the MediatR behaviors — is tagged with the same id, giving traceability without full OpenTelemetry.
/// </summary>
internal static class CorrelationIdExtensions
{
    private const string HeaderName = "X-Correlation-ID";
    private const string LogPropertyName = "CorrelationId";

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            string correlationId =
                context.Request.Headers.TryGetValue(HeaderName, out var inbound) && !string.IsNullOrWhiteSpace(inbound)
                    ? inbound.ToString()
                    : Guid.NewGuid().ToString("N");

            context.Items[HeaderName] = correlationId;

            // Echo the id back so callers (and SignalR clients) can correlate their requests.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty(LogPropertyName, correlationId))
            {
                await next();
            }
        });
    }
}
