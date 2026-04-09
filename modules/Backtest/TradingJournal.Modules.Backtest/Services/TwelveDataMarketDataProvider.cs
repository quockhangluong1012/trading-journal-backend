using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Twelve Data REST API client for downloading historical OHLCV data.
/// Supports Forex (EUR/USD, GBP/USD), Metals (XAU/USD), and Futures (NQ, ES).
///
/// API docs: https://twelvedata.com/docs
/// Free tier: 800 requests/day, 8 requests/minute.
/// Endpoint: GET /time_series
/// </summary>
internal sealed class TwelveDataMarketDataProvider(
    HttpClient httpClient,
    IOptions<TwelveDataOptions> options,
    ILogger<TwelveDataMarketDataProvider> logger) : IMarketDataProvider
{
    private const string BaseUrl = "https://api.twelvedata.com";
    private const int MaxOutputSize = 5000; // Twelve Data max per request

    public async Task<List<OhlcvCandleData>> DownloadOhlcvAsync(
        string asset,
        Timeframe timeframe,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        string symbol = NormalizeSymbol(asset);
        string interval = GetIntervalString(timeframe);
        string apiKey = options.Value.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Twelve Data API key is not configured. Set 'TwelveData:ApiKey' in appsettings.");

        List<OhlcvCandleData> allCandles = [];
        DateTime currentEnd = endDate;

        logger.LogInformation(
            "Downloading {Symbol} {Interval} candles from {Start} to {End} via Twelve Data",
            symbol, interval, startDate, endDate);

        // Paginate backwards: Twelve Data returns newest-first, so we request pages
        // from endDate backwards until we reach startDate
        while (currentEnd > startDate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string startStr = startDate.ToString("yyyy-MM-dd HH:mm:ss");
            string endStr = currentEnd.ToString("yyyy-MM-dd HH:mm:ss");

            string url = $"{BaseUrl}/time_series" +
                         $"?symbol={symbol}" +
                         $"&interval={interval}" +
                         $"&start_date={Uri.EscapeDataString(startStr)}" +
                         $"&end_date={Uri.EscapeDataString(endStr)}" +
                         $"&outputsize={MaxOutputSize}" +
                         $"&format=JSON" +
                         $"&apikey={apiKey}";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                // Check for API errors
                if (root.TryGetProperty("code", out JsonElement codeElement) && codeElement.GetInt32() != 200)
                {
                    string message = root.TryGetProperty("message", out JsonElement msgEl)
                        ? msgEl.GetString() ?? "Unknown error"
                        : "Unknown error";
                    logger.LogError("Twelve Data API error: {Message}", message);
                    throw new HttpRequestException($"Twelve Data API error: {message}");
                }

                if (!root.TryGetProperty("values", out JsonElement valuesElement))
                {
                    logger.LogWarning("No 'values' array in Twelve Data response for {Symbol}", symbol);
                    break;
                }

                List<OhlcvCandleData> pageCandles = [];

                foreach (JsonElement candle in valuesElement.EnumerateArray())
                {
                    string datetimeStr = candle.GetProperty("datetime").GetString()!;
                    DateTime timestamp = DateTime.Parse(datetimeStr, null, System.Globalization.DateTimeStyles.AssumeUniversal)
                        .ToUniversalTime();

                    if (timestamp < startDate) continue;

                    decimal open = decimal.Parse(candle.GetProperty("open").GetString()!);
                    decimal high = decimal.Parse(candle.GetProperty("high").GetString()!);
                    decimal low = decimal.Parse(candle.GetProperty("low").GetString()!);
                    decimal close = decimal.Parse(candle.GetProperty("close").GetString()!);

                    // Volume may not be present for forex
                    decimal volume = 0m;
                    if (candle.TryGetProperty("volume", out JsonElement volEl))
                    {
                        string? volStr = volEl.GetString();
                        if (!string.IsNullOrEmpty(volStr))
                            decimal.TryParse(volStr, out volume);
                    }

                    pageCandles.Add(new OhlcvCandleData(timestamp, open, high, low, close, volume));
                }

                if (pageCandles.Count == 0)
                    break;

                allCandles.AddRange(pageCandles);

                // Twelve Data returns newest first, so the earliest timestamp is the last in the batch
                DateTime earliestInBatch = pageCandles.Min(c => c.Timestamp);

                // If we got fewer than max, we've reached the beginning
                if (pageCandles.Count < MaxOutputSize || earliestInBatch <= startDate)
                    break;

                // Move the window back
                currentEnd = earliestInBatch.AddSeconds(-1);

                logger.LogDebug(
                    "Downloaded {Count} candles page, total so far: {Total}, next end: {NextEnd}",
                    pageCandles.Count, allCandles.Count, currentEnd);

                // Rate limiting: 8 requests/minute = ~7.5 seconds between requests
                // Being conservative with 2 seconds
                await Task.Delay(2000, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Twelve Data API request failed for {Symbol} {Interval}", symbol, interval);
                throw;
            }
        }

        // Sort chronologically (Twelve Data returns newest first)
        allCandles = allCandles
            .DistinctBy(c => c.Timestamp)
            .OrderBy(c => c.Timestamp)
            .ToList();

        logger.LogInformation(
            "Downloaded total {Total} candles for {Symbol} {Interval}",
            allCandles.Count, symbol, interval);

        return allCandles;
    }

    public string GetIntervalString(Timeframe timeframe) => timeframe switch
    {
        Timeframe.M1 => "1min",
        Timeframe.M5 => "5min",
        Timeframe.M15 => "15min",
        Timeframe.H1 => "1h",
        Timeframe.H4 => "4h",
        Timeframe.D1 => "1day",
        _ => throw new ArgumentOutOfRangeException(nameof(timeframe), timeframe, "Unsupported timeframe")
    };

    /// <summary>
    /// Normalizes asset symbols to Twelve Data format.
    /// Supported formats:
    ///   Forex:   EUR/USD, GBP/USD, USD/JPY
    ///   Metals:  XAU/USD (gold), XAG/USD (silver)
    ///   Futures: NQ (Nasdaq E-mini), ES (S&amp;P E-mini), YM (Dow E-mini)
    /// </summary>
    private static string NormalizeSymbol(string asset)
    {
        // Twelve Data uses the standard symbol formats:
        // Forex: EUR/USD (with slash)
        // Futures: NQ, ES (without suffix)
        // Metals: XAU/USD

        string trimmed = asset.Trim().ToUpperInvariant();

        // Map common aliases
        return trimmed switch
        {
            // Twelve Data does NOT support Futures (NQ, ES, YM).
            // We map them to their corresponding Cash Indices instead.
            "NASDAQ" or "NASDAQ E-MINI" or "NQ FUTURES" or "NQ" or "MNQ" or "US100" => "NDX",
            "S&P 500" or "S&P E-MINI" or "ES FUTURES" or "ES" or "MES" or "US500" => "SPX",
            "DOW JONES" or "DOW E-MINI" or "YM FUTURES" or "YM" or "MYM" or "US30" => "DJI",
            
            "GOLD" or "XAUUSD" => "XAU/USD",
            "SILVER" or "XAGUSD" => "XAG/USD",
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
            _ => trimmed // Use as-is for already-formatted symbols
        };
    }
}

/// <summary>
/// Configuration options for Twelve Data API.
/// </summary>
public sealed class TwelveDataOptions
{
    public const string SectionName = "TwelveData";

    public string ApiKey { get; set; } = string.Empty;
}
