using System.Net;

namespace TradingJournal.ApiGateWay.Extensions;

/// <summary>
/// Startup configuration helpers extracted from Program.cs to keep the entry point clean.
/// </summary>
internal static class StartupConfigurationExtensions
{
    public static string GetRequiredConfigurationValue(this IConfiguration configuration, string key)
    {
        string? value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{key}' is required.");
        }

        return value;
    }

    public static void ValidateJwtConfiguration(string secret, string issuer, string audience)
    {
        if (secret.Length < 32)
        {
            throw new InvalidOperationException("JWT Secret must be at least 32 characters long.");
        }

        if (secret.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase) ||
            issuer.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase) ||
            audience.StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("JWT configuration contains unreplaced placeholder values.");
        }
    }

    public static string[] GetAllowedOrigins(this IConfiguration configuration)
    {
        return CorsOriginNormalizer.Normalize(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []);
    }

    public static int GetPositiveIntConfigurationValue(this IConfiguration configuration, string key, int fallback)
    {
        int value = configuration.GetValue<int?>(key) ?? fallback;
        if (value <= 0)
        {
            throw new InvalidOperationException($"Configuration value '{key}' must be greater than zero.");
        }

        return value;
    }

    public static string GetClientIpAddress(HttpContext httpContext)
    {
        IPAddress? remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp is not null && IsPrivateOrLoopback(remoteIp) &&
            httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            string? forwardedIp = forwardedFor.ToString()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(forwardedIp) && IPAddress.TryParse(forwardedIp, out IPAddress? parsedForwardedIp))
            {
                return parsedForwardedIp.ToString();
            }
        }

        return remoteIp?.ToString() ?? "unknown";
    }

    private static bool IsPrivateOrLoopback(IPAddress address)
    {
        IPAddress normalizedAddress = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        if (IPAddress.IsLoopback(normalizedAddress))
        {
            return true;
        }

        byte[] bytes = normalizedAddress.GetAddressBytes();

        if (normalizedAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168);
        }

        return normalizedAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
               (normalizedAddress.IsIPv6LinkLocal || normalizedAddress.IsIPv6SiteLocal || bytes[0] == 0xfc || bytes[0] == 0xfd);
    }
}
