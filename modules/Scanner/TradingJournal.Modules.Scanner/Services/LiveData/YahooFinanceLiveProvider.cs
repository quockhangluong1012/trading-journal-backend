using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingJournal.Modules.Scanner.Services.ICTAnalysis;

namespace TradingJournal.Modules.Scanner.Services.LiveData;

/// <summary>
/// Fetches live OHLCV candle data from Yahoo Finance's free v8 chart API.
/// Replaces TwelveData which requires paid plans for NASDAQ/index symbols.
///
/// Includes an in-memory cache with TTL scaled to each timeframe.
///
/// API endpoint: GET https://query1.finance.yahoo.com/v8/finance/chart/{symbol}
/// No API key required. Completely free for all symbol types.
/// </summary>
internal sealed class YahooFinanceLiveProvider(
    HttpClient httpClient,
    ILogger<YahooFinanceLiveProvider> logger) : ILiveMarketDataProvider
{
    private const string BaseUrl = "https://query1.finance.yahoo.com/v8/finance/chart";

    /// <summary>
    /// In-memory cache: keyed by (Symbol, Timeframe) → (Candles, ExpiresAtUtc).
    /// </summary>
    private static readonly ConcurrentDictionary<(string, ScannerTimeframe), (List<CandleData> Candles, DateTimeOffset ExpiresAt)>
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
            cached.ExpiresAt > DateTimeOffset.UtcNow &&
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
                Cache[(cacheKey, timeframe)] = (candles, DateTimeOffset.UtcNow + ttl);

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
                    symbol, timeframe, DateTimeOffset.UtcNow - stale.ExpiresAt);
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
        string yahooSymbol = NormalizeSymbol(symbol);
        string interval = MapInterval(timeframe);
        string range = MapRange(timeframe, count);

        string url = $"{BaseUrl}/{Uri.EscapeDataString(yahooSymbol)}" +
                     $"?interval={interval}" +
                     $"&range={range}" +
                     $"&includePrePost=false";

        logger.LogDebug("Fetching Yahoo Finance data: {Symbol} ({YahooSymbol}) {Interval} range={Range}",
            symbol, yahooSymbol, interval, range);

        // Yahoo Finance requires a User-Agent header
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) TradingJournal/1.0");

        HttpResponseMessage response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        // Check for API-level errors
        if (root.TryGetProperty("chart", out JsonElement chartEl) &&
            chartEl.TryGetProperty("error", out JsonElement errorEl) &&
            errorEl.ValueKind != JsonValueKind.Null)
        {
            string errMsg = errorEl.TryGetProperty("description", out JsonElement descEl)
                ? descEl.GetString() ?? "Unknown error"
                : "Unknown error";

            logger.LogError("Yahoo Finance API error for {Symbol}: {Error}", symbol, errMsg);
            throw new HttpRequestException($"Yahoo Finance API error: {errMsg}");
        }

        // Navigate to the result data
        if (!root.TryGetProperty("chart", out JsonElement chart) ||
            !chart.TryGetProperty("result", out JsonElement resultArr) ||
            resultArr.GetArrayLength() == 0)
        {
            logger.LogWarning("No chart result in Yahoo Finance response for {Symbol}", symbol);
            return [];
        }

        JsonElement result = resultArr[0];

        if (!result.TryGetProperty("timestamp", out JsonElement timestampArr))
        {
            logger.LogWarning("No timestamp array in Yahoo Finance response for {Symbol}", symbol);
            return [];
        }

        if (!result.TryGetProperty("indicators", out JsonElement indicators) ||
            !indicators.TryGetProperty("quote", out JsonElement quoteArr) ||
            quoteArr.GetArrayLength() == 0)
        {
            logger.LogWarning("No quote indicators in Yahoo Finance response for {Symbol}", symbol);
            return [];
        }

        JsonElement quote = quoteArr[0];
        JsonElement opens = quote.GetProperty("open");
        JsonElement highs = quote.GetProperty("high");
        JsonElement lows = quote.GetProperty("low");
        JsonElement closes = quote.GetProperty("close");

        // Volume is optional (forex may not have it)
        bool hasVolume = quote.TryGetProperty("volume", out JsonElement volumes);

        var candles = new List<CandleData>();
        int length = timestampArr.GetArrayLength();

        for (int i = 0; i < length; i++)
        {
            // Skip null entries (market closed periods)
            if (opens[i].ValueKind == JsonValueKind.Null ||
                highs[i].ValueKind == JsonValueKind.Null ||
                lows[i].ValueKind == JsonValueKind.Null ||
                closes[i].ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            long unixTimestamp = timestampArr[i].GetInt64();
            DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;

            decimal open = GetDecimal(opens[i]);
            decimal high = GetDecimal(highs[i]);
            decimal low = GetDecimal(lows[i]);
            decimal close = GetDecimal(closes[i]);

            decimal volume = 0m;
            if (hasVolume && i < volumes.GetArrayLength() && volumes[i].ValueKind != JsonValueKind.Null)
            {
                volume = GetDecimal(volumes[i]);
            }

            candles.Add(new CandleData(timestamp, open, high, low, close, volume));
        }

        return candles;
    }

    /// <summary>
    /// Safely extracts a decimal from a JSON element that may be a number or string.
    /// </summary>
    private static decimal GetDecimal(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDecimal(),
            JsonValueKind.String => decimal.Parse(el.GetString()!, CultureInfo.InvariantCulture),
            _ => 0m
        };
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
        ScannerTimeframe.M5 => "5m",
        ScannerTimeframe.M15 => "15m",
        ScannerTimeframe.H1 => "1h",
        ScannerTimeframe.D1 => "1d",
        _ => throw new ArgumentOutOfRangeException(nameof(tf), tf, "Unsupported scanner timeframe")
    };

    /// <summary>
    /// Maps timeframe + count to Yahoo Finance range parameter.
    /// We fetch extra data to ensure we have enough after filtering out market closures.
    /// </summary>
    private static string MapRange(ScannerTimeframe tf, int count) => tf switch
    {
        ScannerTimeframe.M5 => "5d",       // 5 days × ~78 bars/day = ~390 bars
        ScannerTimeframe.M15 => "15d",      // 15 days × ~26 bars/day = ~390 bars
        ScannerTimeframe.H1 => "60d",       // 60 days × ~7 bars/day = ~420 bars
        ScannerTimeframe.D1 => "1y",        // 365 daily bars
        _ => "5d"
    };

    /// <summary>
    /// Normalizes symbols to Yahoo Finance format.
    /// Yahoo Finance uses different conventions:
    ///   Futures:  NQ=F, ES=F, YM=F
    ///   Indices:  ^IXIC (NASDAQ), ^GSPC (S&P 500), ^DJI (Dow Jones)
    ///   Forex:    EURUSD=X
    ///   Metals:   GC=F (Gold), SI=F (Silver)
    ///   DXY:      DX-Y.NYB
    /// </summary>
    private static string NormalizeSymbol(string asset)
    {
        string trimmed = asset.Trim().ToUpperInvariant();

        return trimmed switch
        {
            // Futures (NQ/ES/YM) → Yahoo Finance futures symbols
            "NASDAQ" or "NASDAQ E-MINI" or "NQ FUTURES" or "NQ" => "NQ=F",
            "MNQ" => "MNQ=F",
            "US100" or "NDX" => "^NDX",

            "S&P 500" or "S&P E-MINI" or "ES FUTURES" or "ES" => "ES=F",
            "MES" => "MES=F",
            "US500" or "SPX" => "^GSPC",

            "DOW JONES" or "DOW E-MINI" or "YM FUTURES" or "YM" => "YM=F",
            "MYM" => "MYM=F",
            "US30" or "DJI" => "^DJI",

            // Metals → Futures contracts
            "GOLD" or "XAUUSD" or "XAU/USD" => "GC=F",
            "SILVER" or "XAGUSD" or "XAG/USD" => "SI=F",

            // Dollar Index
            "DXY" or "USDX" => "DX-Y.NYB",

            // Forex → Yahoo format (EURUSD=X)
            "EURUSD" or "EUR/USD" => "EURUSD=X",
            "GBPUSD" or "GBP/USD" => "GBPUSD=X",
            "USDJPY" or "USD/JPY" => "USDJPY=X",
            "AUDUSD" or "AUD/USD" => "AUDUSD=X",
            "USDCAD" or "USD/CAD" => "USDCAD=X",
            "USDCHF" or "USD/CHF" => "USDCHF=X",
            "NZDUSD" or "NZD/USD" => "NZDUSD=X",
            "GBPJPY" or "GBP/JPY" => "GBPJPY=X",
            "EURJPY" or "EUR/JPY" => "EURJPY=X",
            "EURGBP" or "EUR/GBP" => "EURGBP=X",

            // Crypto pass-through
            _ => trimmed
        };
    }
}
