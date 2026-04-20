namespace TradingJournal.ApiGateWay.Extensions;

public static class CorsOriginNormalizer
{
    public static string[] Normalize(IEnumerable<string?> configuredOrigins)
    {
        ArgumentNullException.ThrowIfNull(configuredOrigins);

        string[] origins = configuredOrigins
            .Where(static origin => !string.IsNullOrWhiteSpace(origin))
            .Select(static origin => NormalizeOrigin(origin!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (origins.Length == 0)
        {
            throw new InvalidOperationException("At least one CORS allowed origin must be configured.");
        }

        return origins;
    }

    private static string NormalizeOrigin(string configuredOrigin)
    {
        string trimmedOrigin = configuredOrigin.Trim();

        if (!Uri.TryCreate(trimmedOrigin, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"CORS origin '{configuredOrigin}' must be an absolute http or https URL.");
        }

        if (uri.PathAndQuery is not "/" || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"CORS origin '{configuredOrigin}' must not include a path, query string, or fragment.");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}