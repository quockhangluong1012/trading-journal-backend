using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.Scanner.Services.EconomicCalendar;

/// <summary>
/// Fetches economic calendar data from the Forex Factory JSON feed.
/// Completely free, no API key needed. Rate-limited to 2 requests per 5 minutes.
///
/// Data source: https://nfs.faireconomy.media/ff_calendar_thisweek.json
/// Provides this week's economic events with impact levels.
///
/// Caching strategy:
///   - Fetches at most once every 3 hours (well within rate limits)
///   - Falls back to stale cache on API failures
///   - Clears cache daily at midnight UTC
/// </summary>
internal sealed class EconomicCalendarProvider(
    HttpClient httpClient,
    ILogger<EconomicCalendarProvider> logger) : IEconomicCalendarProvider
{
    private const string ThisWeekUrl = "https://nfs.faireconomy.media/ff_calendar_thisweek.json";

    /// <summary>
    /// Cached weekly events with expiration.
    /// </summary>
    private static (List<EconomicEvent> Events, DateTime ExpiresAt, DateOnly FetchDate)? _weeklyCache;

    /// <summary>
    /// Lock for thread-safe cache access.
    /// </summary>
    private static readonly SemaphoreSlim CacheLock = new(1, 1);

    /// <summary>
    /// Cache TTL — 3 hours is well within the 2-requests-per-5-minutes rate limit.
    /// Calendar data is only updated hourly by the source, so 3 hours is safe.
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(3);

    public async Task<List<EconomicEvent>> GetEventsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        // Forex Factory feed only provides current week data.
        // For date ranges, we filter the cached weekly events.
        List<EconomicEvent> allEvents = await FetchWeeklyEventsAsync(ct);

        return allEvents
            .Where(e =>
            {
                DateOnly eventDate = DateOnly.FromDateTime(e.EventDateUtc);
                return eventDate >= from && eventDate <= to;
            })
            .OrderBy(e => e.EventDateUtc)
            .ToList();
    }

    public async Task<List<EconomicEvent>> GetTodayEventsAsync(CancellationToken ct = default)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await GetEventsAsync(today, today, ct);
    }

    public async Task<List<EconomicEvent>> GetUpcomingHighImpactEventsAsync(
        TimeSpan lookAheadWindow,
        CancellationToken ct = default)
    {
        List<EconomicEvent> todayEvents = await GetTodayEventsAsync(ct);

        DateTime now = DateTime.UtcNow;
        DateTime windowEnd = now + lookAheadWindow;

        return todayEvents
            .Where(e => e.Impact == EconomicImpact.High &&
                        e.EventDateUtc >= now &&
                        e.EventDateUtc <= windowEnd)
            .OrderBy(e => e.EventDateUtc)
            .ToList();
    }

    public async Task RefreshTodayCacheAsync(CancellationToken ct = default)
    {
        // Force re-fetch by clearing cache
        await CacheLock.WaitAsync(ct);
        try
        {
            _weeklyCache = null;
        }
        finally
        {
            CacheLock.Release();
        }

        await FetchWeeklyEventsAsync(ct);
    }

    /// <summary>
    /// Fetches this week's events, using cache when available.
    /// </summary>
    private async Task<List<EconomicEvent>> FetchWeeklyEventsAsync(CancellationToken ct)
    {
        // Fast path: check cache without locking
        var snapshot = _weeklyCache;
        if (snapshot.HasValue && snapshot.Value.ExpiresAt > DateTime.UtcNow)
        {
            return snapshot.Value.Events;
        }

        await CacheLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            snapshot = _weeklyCache;
            if (snapshot.HasValue && snapshot.Value.ExpiresAt > DateTime.UtcNow)
            {
                return snapshot.Value.Events;
            }

            // Fetch from API
            try
            {
                List<EconomicEvent> events = await FetchFromApiAsync(ct);

                _weeklyCache = (events, DateTime.UtcNow + CacheTtl, DateOnly.FromDateTime(DateTime.UtcNow));

                logger.LogInformation(
                    "Economic calendar: Fetched {Count} events from Forex Factory feed, cached for {Ttl}",
                    events.Count, CacheTtl);

                return events;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Economic calendar: Failed to fetch from Forex Factory feed");

                // Return stale cache if available
                if (snapshot.HasValue && snapshot.Value.Events.Count > 0)
                {
                    logger.LogWarning("Economic calendar: Using stale cache ({Count} events)", snapshot.Value.Events.Count);
                    return snapshot.Value.Events;
                }

                return [];
            }
        }
        finally
        {
            CacheLock.Release();
        }
    }

    /// <summary>
    /// Fetches the raw JSON from the Forex Factory feed and parses it.
    ///
    /// Expected JSON format:
    /// [
    ///   {
    ///     "title": "Non-Farm Employment Change",
    ///     "country": "USD",
    ///     "date": "2024-01-05T13:30:00-05:00",
    ///     "impact": "High",
    ///     "forecast": "170K",
    ///     "previous": "199K"
    ///   }
    /// ]
    /// </summary>
    private async Task<List<EconomicEvent>> FetchFromApiAsync(CancellationToken ct)
    {
        logger.LogDebug("Economic calendar: Fetching from Forex Factory feed...");

        using var request = new HttpRequestMessage(HttpMethod.Get, ThisWeekUrl);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) TradingJournal/1.0");
        request.Headers.Add("Accept", "application/json");

        HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
        {
            logger.LogWarning("Economic calendar: Unexpected response format (expected array)");
            return [];
        }

        var events = new List<EconomicEvent>();

        foreach (JsonElement item in root.EnumerateArray())
        {
            try
            {
                string title = item.TryGetProperty("title", out JsonElement titleEl)
                    ? titleEl.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(title)) continue;

                string dateStr = item.TryGetProperty("date", out JsonElement dateEl)
                    ? dateEl.GetString() ?? ""
                    : "";

                if (string.IsNullOrWhiteSpace(dateStr) ||
                    !DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTimeOffset eventDateOffset))
                {
                    continue;
                }

                DateTime eventDateUtc = eventDateOffset.UtcDateTime;

                // "country" in Forex Factory feed is actually the currency code (e.g., "USD", "EUR")
                string currency = item.TryGetProperty("country", out JsonElement countryEl)
                    ? countryEl.GetString() ?? ""
                    : "";

                string country = MapCurrencyToCountry(currency);

                string impactStr = item.TryGetProperty("impact", out JsonElement impEl)
                    ? impEl.GetString() ?? "Low"
                    : "Low";

                EconomicImpact impact = ParseImpact(impactStr);

                // Forecast and Previous are strings with units (e.g., "170K", "3.7%")
                string? forecastStr = item.TryGetProperty("forecast", out JsonElement fcEl)
                    ? fcEl.GetString()
                    : null;

                string? previousStr = item.TryGetProperty("previous", out JsonElement prevEl)
                    ? prevEl.GetString()
                    : null;

                decimal? forecast = ParseValueString(forecastStr);
                decimal? previous = ParseValueString(previousStr);

                // Extract unit from the value string
                string? unit = ExtractUnit(forecastStr ?? previousStr);

                // Generate stable ID
                string id = $"{currency}_{title}_{eventDateUtc:yyyyMMddHHmm}"
                    .Replace(" ", "_")
                    .ToUpperInvariant();

                events.Add(new EconomicEvent
                {
                    Id = id,
                    Country = country,
                    Currency = currency,
                    EventName = title,
                    EventDateUtc = eventDateUtc,
                    Impact = impact,
                    Actual = null, // Forex Factory feed doesn't include actual in the JSON
                    Forecast = forecast,
                    Previous = previous,
                    Unit = unit
                });
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Economic calendar: Skipping malformed event entry");
            }
        }

        return events.OrderBy(e => e.EventDateUtc).ToList();
    }

    /// <summary>
    /// Parses impact string from Forex Factory.
    /// Values: "High", "Medium", "Low", "Holiday", "Non-Economic"
    /// </summary>
    private static EconomicImpact ParseImpact(string impact) => impact.ToLowerInvariant() switch
    {
        "high" => EconomicImpact.High,
        "medium" => EconomicImpact.Medium,
        _ => EconomicImpact.Low  // Low, Holiday, Non-Economic all map to Low
    };

    /// <summary>
    /// Parses value strings like "170K", "3.7%", "-0.2%", "1.25M" into decimal values.
    /// Returns null if unparseable.
    /// </summary>
    private static decimal? ParseValueString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        string cleaned = value.Trim();

        // Remove common suffixes
        decimal multiplier = 1m;
        if (cleaned.EndsWith("%", StringComparison.Ordinal))
        {
            cleaned = cleaned[..^1].Trim();
        }
        else if (cleaned.EndsWith("K", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^1].Trim();
            multiplier = 1_000m;
        }
        else if (cleaned.EndsWith("M", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^1].Trim();
            multiplier = 1_000_000m;
        }
        else if (cleaned.EndsWith("B", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^1].Trim();
            multiplier = 1_000_000_000m;
        }
        else if (cleaned.EndsWith("T", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^1].Trim();
            multiplier = 1_000_000_000_000m;
        }

        // Handle "|" pipe character sometimes present
        if (cleaned.Contains("|", StringComparison.Ordinal))
        {
            cleaned = cleaned.Split('|')[0].Trim();
        }

        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
        {
            return result * multiplier;
        }

        return null;
    }

    /// <summary>
    /// Extracts the unit suffix from a value string (e.g., "%" from "3.7%", "K" from "170K").
    /// </summary>
    private static string? ExtractUnit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        string trimmed = value.Trim();
        if (trimmed.EndsWith("%", StringComparison.Ordinal)) return "%";
        if (trimmed.EndsWith("K", StringComparison.OrdinalIgnoreCase)) return "K";
        if (trimmed.EndsWith("M", StringComparison.OrdinalIgnoreCase)) return "M";
        if (trimmed.EndsWith("B", StringComparison.OrdinalIgnoreCase)) return "B";
        if (trimmed.EndsWith("T", StringComparison.OrdinalIgnoreCase)) return "T";

        return null;
    }

    /// <summary>
    /// Maps currency code to country code.
    /// Forex Factory uses currency as the "country" field.
    /// </summary>
    private static string MapCurrencyToCountry(string currency) => currency.ToUpperInvariant() switch
    {
        "USD" => "US",
        "EUR" => "EU",
        "GBP" => "GB",
        "JPY" => "JP",
        "AUD" => "AU",
        "CAD" => "CA",
        "CHF" => "CH",
        "NZD" => "NZ",
        "CNY" => "CN",
        _ => currency
    };
}
