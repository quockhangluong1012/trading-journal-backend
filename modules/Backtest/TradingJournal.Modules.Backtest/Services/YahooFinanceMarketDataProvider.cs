using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Yahoo Finance REST API client for downloading historical OHLCV data.
/// Replaces TwelveData which requires paid plans for NASDAQ/index symbols.
///
/// Supports Forex, Metals, Futures, Indices, and Crypto — all completely free.
///
/// API endpoint: GET https://query1.finance.yahoo.com/v8/finance/chart/{symbol}
/// No API key required.
/// </summary>
internal sealed class YahooFinanceMarketDataProvider(
    HttpClient httpClient,
    ILogger<YahooFinanceMarketDataProvider> logger) : IMarketDataProvider
{
    private const string BaseUrl = "https://query1.finance.yahoo.com/v8/finance/chart";

    public async Task<List<OhlcvCandleData>> DownloadOhlcvAsync(
        string asset,
        Timeframe timeframe,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        string symbol = NormalizeSymbol(asset);
        string interval = GetIntervalString(timeframe);

        // Yahoo Finance uses Unix timestamps for period1/period2
        long period1 = new DateTimeOffset(startDate.ToUniversalTime()).ToUnixTimeSeconds();
        long period2 = new DateTimeOffset(endDate.ToUniversalTime()).ToUnixTimeSeconds();

        logger.LogInformation(
            "Downloading {Symbol} {Interval} candles from {Start} to {End} via Yahoo Finance",
            symbol, interval, startDate, endDate);

        List<OhlcvCandleData> allCandles = [];

        try
        {
            string url = $"{BaseUrl}/{Uri.EscapeDataString(symbol)}" +
                         $"?period1={period1}" +
                         $"&period2={period2}" +
                         $"&interval={interval}" +
                         $"&includePrePost=false" +
                         $"&events=history";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) TradingJournal/1.0");

            HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(cancellationToken);
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
            bool hasVolume = quote.TryGetProperty("volume", out JsonElement volumes);

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
                DateTime timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;

                // Filter to requested range
                if (timestamp < startDate) continue;

                decimal open = GetDecimal(opens[i]);
                decimal high = GetDecimal(highs[i]);
                decimal low = GetDecimal(lows[i]);
                decimal close = GetDecimal(closes[i]);

                decimal volume = 0m;
                if (hasVolume && i < volumes.GetArrayLength() && volumes[i].ValueKind != JsonValueKind.Null)
                {
                    volume = GetDecimal(volumes[i]);
                }

                allCandles.Add(new OhlcvCandleData(timestamp, open, high, low, close, volume));
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Yahoo Finance API request failed for {Symbol} {Interval}", symbol, interval);
            throw;
        }

        // Sort chronologically and deduplicate
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
        Timeframe.M1 => "1m",
        Timeframe.M5 => "5m",
        Timeframe.M15 => "15m",
        Timeframe.H1 => "1h",
        Timeframe.H4 => "1h",  // Yahoo doesn't support 4h natively; fetch 1h and aggregate
        Timeframe.D1 => "1d",
        _ => throw new ArgumentOutOfRangeException(nameof(timeframe), timeframe, "Unsupported timeframe")
    };

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
    /// Normalizes asset symbols to Yahoo Finance format.
    /// Yahoo Finance uses different conventions than other providers:
    ///   Futures:  NQ=F, ES=F, YM=F
    ///   Indices:  ^IXIC (NASDAQ Composite), ^GSPC (S&amp;P 500), ^DJI (Dow)
    ///   Forex:    EURUSD=X
    ///   Metals:   GC=F (Gold futures), SI=F (Silver futures)
    ///   DXY:      DX-Y.NYB
    /// </summary>
    private static string NormalizeSymbol(string asset)
    {
        string trimmed = asset.Trim().ToUpperInvariant();

        return trimmed switch
        {
            // Futures → Yahoo Finance futures symbols
            "NASDAQ" or "NASDAQ E-MINI" or "NQ FUTURES" or "NQ" => "NQ=F",
            "MNQ" => "MNQ=F",
            "US100" or "NDX" => "^NDX",

            "S&P 500" or "S&P E-MINI" or "ES FUTURES" or "ES" => "ES=F",
            "MES" => "MES=F",
            "US500" or "SPX" => "^GSPC",

            "DOW JONES" or "DOW E-MINI" or "YM FUTURES" or "YM" => "YM=F",
            "MYM" => "MYM=F",
            "US30" or "DJI" => "^DJI",

            // Metals
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

            // Pass-through for already-formatted symbols
            _ => trimmed
        };
    }
}
