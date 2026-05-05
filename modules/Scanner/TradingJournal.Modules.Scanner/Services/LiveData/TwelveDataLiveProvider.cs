using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingJournal.Modules.Scanner.Services.ICTAnalysis;

namespace TradingJournal.Modules.Scanner.Services.LiveData;

/// <summary>
/// Fetches live OHLCV candle data from Twelve Data REST API.
/// Includes an in-memory cache with TTL scaled to each timeframe to respect
/// the free-tier rate limits (8 req/min, 800 req/day).
///
/// API docs: https://twelvedata.com/docs#time-series
/// Endpoint: GET /time_series
/// </summary>
internal sealed class TwelveDataLiveProvider(
    HttpClient httpClient,
    IOptions<TwelveDataLiveOptions> options,
    ILogger<TwelveDataLiveProvider> logger) : ILiveMarketDataProvider
{
    private const string BaseUrl = "https://api.twelvedata.com";

    /// <summary>
    /// In-memory cache: keyed by (Symbol, Timeframe) → (Candles, ExpiresAtUtc).
    /// </summary>
    private static readonly ConcurrentDictionary<(string, ScannerTimeframe), (List<CandleData> Candles, DateTime ExpiresAt)>
        Cache = new();

    public async Task<List<CandleData>> GetRecentCandlesAsync(
        string symbol,
        ScannerTimeframe timeframe,
        int count,
        CancellationToken ct = default)
    {
        string cacheKey = symbol.ToUpperInvariant();

        // Check cache first
        if (Cache.TryGetValue((cacheKey, timeframe), out var cached) &&
            cached.ExpiresAt > DateTime.UtcNow &&
            cached.Candles.Count >= count)
        {
            logger.LogDebug("Cache hit for {Symbol} {Timeframe} ({Count} candles)",
                symbol, timeframe, cached.Candles.Count);
            return cached.Candles.TakeLast(count).ToList();
        }

        // Fetch from API
        try
        {
            List<CandleData> candles = await FetchFromApiAsync(symbol, timeframe, count, ct);

            if (candles.Count > 0)
            {
                TimeSpan ttl = GetCacheTtl(timeframe);
                Cache[(cacheKey, timeframe)] = (candles, DateTime.UtcNow + ttl);

                logger.LogInformation(
                    "Fetched {Count} live candles for {Symbol} {Timeframe}, cached for {Ttl}",
                    candles.Count, symbol, timeframe, ttl);
            }

            return candles;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch live candles for {Symbol} {Timeframe}", symbol, timeframe);

            // Fall back to stale cache if available
            if (Cache.TryGetValue((cacheKey, timeframe), out var stale) && stale.Candles.Count > 0)
            {
                logger.LogWarning("Using stale cache for {Symbol} {Timeframe} (expired {Ago} ago)",
                    symbol, timeframe, DateTime.UtcNow - stale.ExpiresAt);
                return stale.Candles.TakeLast(count).ToList();
            }

            return [];
        }
    }

    private async Task<List<CandleData>> FetchFromApiAsync(
        string symbol,
        ScannerTimeframe timeframe,
        int count,
        CancellationToken ct)
    {
        string apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Twelve Data API key is not configured. Set 'TwelveData:ApiKey' in appsettings.");
        }

        string normalizedSymbol = NormalizeSymbol(symbol);
        string interval = MapInterval(timeframe);

        // Fetch slightly more than needed to account for market closures / gaps
        int outputSize = Math.Min(count + 10, 5000);

        string url = $"{BaseUrl}/time_series" +
                     $"?symbol={Uri.EscapeDataString(normalizedSymbol)}" +
                     $"&interval={interval}" +
                     $"&outputsize={outputSize}" +
                     $"&format=JSON" +
                     $"&apikey={apiKey}";

        logger.LogDebug("Fetching live data: {Symbol} ({Normalized}) {Interval} x{Count}",
            symbol, normalizedSymbol, interval, count);

        HttpResponseMessage response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        // Check for API-level errors
        if (root.TryGetProperty("code", out JsonElement codeEl) && codeEl.GetInt32() != 200)
        {
            string errMsg = root.TryGetProperty("message", out JsonElement msgEl)
                ? msgEl.GetString() ?? "Unknown error"
                : "Unknown error";

            logger.LogError("Twelve Data API error for {Symbol}: {Error}", symbol, errMsg);
            throw new HttpRequestException($"Twelve Data API error: {errMsg}");
        }

        if (!root.TryGetProperty("values", out JsonElement valuesEl))
        {
            logger.LogWarning("No 'values' array in Twelve Data response for {Symbol}", symbol);
            return [];
        }

        var candles = new List<CandleData>();

        foreach (JsonElement candle in valuesEl.EnumerateArray())
        {
            string datetimeStr = candle.GetProperty("DateTime").GetString()!;
            DateTime timestamp = DateTime.Parse(datetimeStr, null, DateTimeStyles.AssumeUniversal)
                .ToUniversalTime();

            decimal open = decimal.Parse(candle.GetProperty("open").GetString()!, CultureInfo.InvariantCulture);
            decimal high = decimal.Parse(candle.GetProperty("high").GetString()!, CultureInfo.InvariantCulture);
            decimal low = decimal.Parse(candle.GetProperty("low").GetString()!, CultureInfo.InvariantCulture);
            decimal close = decimal.Parse(candle.GetProperty("close").GetString()!, CultureInfo.InvariantCulture);

            decimal volume = 0m;
            if (candle.TryGetProperty("volume", out JsonElement volEl))
            {
                string? volStr = volEl.GetString();
                if (!string.IsNullOrEmpty(volStr))
                    decimal.TryParse(volStr, CultureInfo.InvariantCulture, out volume);
            }

            candles.Add(new CandleData(timestamp, open, high, low, close, volume));
        }

        // Twelve Data returns newest-first; reverse to chronological order
        candles.Reverse();

        return candles;
    }

    /// <summary>
    /// Cache TTL scales with the timeframe to minimize API calls:
    /// - M5 candles change every 5 min → cache 4 min
    /// - M15 → cache 12 min
    /// - H1 → cache 45 min
    /// - D1 → cache 6 hours
    /// </summary>
    private static TimeSpan GetCacheTtl(ScannerTimeframe tf) => tf switch
    {
        ScannerTimeframe.M5 => TimeSpan.FromMinutes(4),
        ScannerTimeframe.M15 => TimeSpan.FromMinutes(12),
        ScannerTimeframe.H1 => TimeSpan.FromMinutes(45),
        ScannerTimeframe.D1 => TimeSpan.FromHours(6),
        _ => TimeSpan.FromMinutes(5)
    };

    private static string MapInterval(ScannerTimeframe tf) => tf switch
    {
        ScannerTimeframe.M5 => "5min",
        ScannerTimeframe.M15 => "15min",
        ScannerTimeframe.H1 => "1h",
        ScannerTimeframe.D1 => "1day",
        _ => throw new ArgumentOutOfRangeException(nameof(tf), tf, "Unsupported scanner timeframe")
    };

    /// <summary>
    /// Normalizes symbols to Twelve Data format.
    /// Normalizes symbols to Twelve Data format.
    /// </summary>
    private static string NormalizeSymbol(string asset)
    {
        string trimmed = asset.Trim().ToUpperInvariant();

        return trimmed switch
        {
            // Futures → Cash Indices (Twelve Data doesn't support futures directly)
            "NASDAQ" or "NASDAQ E-MINI" or "NQ FUTURES" or "NQ" or "MNQ" or "US100" => "NDX",
            "S&P 500" or "S&P E-MINI" or "ES FUTURES" or "ES" or "MES" or "US500" => "SPX",
            "DOW JONES" or "DOW E-MINI" or "YM FUTURES" or "YM" or "MYM" or "US30" => "DJI",

            // Metals
            "GOLD" or "XAUUSD" => "XAU/USD",
            "SILVER" or "XAGUSD" => "XAG/USD",

            // Dollar Index
            "DXY" or "USDX" => "DXY",

            // Forex — add slash if missing
            "EURUSD" => "EUR/USD",
            "GBPUSD" => "GBP/USD",
            "USDJPY" => "USD/JPY",
            "AUDUSD" => "AUD/USD",
            "USDCAD" => "USD/CAD",
            "USDCHF" => "USD/CHF",
            "NZDUSD" => "NZD/USD",
            "GBPJPY" => "GBP/JPY",
            "EURJPY" => "EUR/JPY",
            "EURGBP" => "EUR/GBP",

            // Crypto pass-through (for future Binance integration)
            _ => trimmed
        };
    }
}

/// <summary>
/// Configuration for the live market data provider.
/// Reads from the existing "TwelveData" appsettings section.
/// </summary>
public sealed class TwelveDataLiveOptions
{
    public const string SectionName = "TwelveData";

    public string ApiKey { get; set; } = string.Empty;
}
