using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TradingJournal.Shared.Idempotency;

namespace TradingJournal.Shared.Middlewares;

/// <summary>
/// Extension method for registering the idempotency middleware.
/// </summary>
public static class IdempotencyMiddlewareExtensions
{
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app)
    {
        return app.UseMiddleware<IdempotencyMiddleware>();
    }
}

/// <summary>
/// Middleware that intercepts mutating HTTP methods (POST, PUT, PATCH, DELETE)
/// and checks for an <c>Idempotency-Key</c> header. When present:
///   1. If the key was seen before → return the cached response immediately.
///   2. If the key is new → execute the request, cache the response, return it.
///
/// GET/HEAD/OPTIONS requests are always passed through without idempotency checks.
/// Requests without the header are also passed through (opt-in model).
///
/// TTL: Cached responses expire after 24 hours.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyStore store)
    {
        // Only apply to mutating methods
        if (!MutatingMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // If no idempotency key header, pass through (opt-in)
        if (!context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            await _next(context);
            return;
        }

        string idempotencyKey = keyValues.First()!.Trim();

        // Validate key format (must be <= 128 chars)
        if (idempotencyKey.Length > 128)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Idempotency-Key must be 128 characters or fewer." });
            return;
        }

        // Check for existing cached response
        IdempotencyRecord? existing = await store.GetAsync(idempotencyKey, context.RequestAborted);

        if (existing is not null)
        {
            _logger.LogInformation("Idempotency cache hit for key: {Key}", idempotencyKey);

            context.Response.StatusCode = existing.StatusCode;

            if (!string.IsNullOrEmpty(existing.ContentType))
            {
                context.Response.ContentType = existing.ContentType;
            }

            if (!string.IsNullOrEmpty(existing.ResponseBody))
            {
                await context.Response.WriteAsync(existing.ResponseBody);
            }

            return;
        }

        // Execute the request and capture the response
        Stream originalBody = context.Response.Body;

        using MemoryStream memoryStream = new();
        context.Response.Body = memoryStream;

        await _next(context);

        // Read the captured response
        memoryStream.Position = 0;
        string responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

        // Store the response for future duplicate requests
        DateTime now = DateTime.UtcNow;
        var record = new IdempotencyRecord
        {
            IdempotencyKey = idempotencyKey,
            StatusCode = context.Response.StatusCode,
            ResponseBody = responseBody,
            ContentType = context.Response.ContentType,
            CreatedAt = now,
            ExpiresAt = now + CacheTtl
        };

        bool saved = await store.SaveAsync(record, context.RequestAborted);

        if (saved)
        {
            _logger.LogDebug("Idempotency response cached for key: {Key}, TTL: {Ttl}", idempotencyKey, CacheTtl);
        }

        // Write the response to the original body
        memoryStream.Position = 0;
        await memoryStream.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }
}
