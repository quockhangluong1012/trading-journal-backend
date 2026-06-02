using System.Globalization;

namespace TradingJournal.Modules.Backtest.Services;

internal static class CsvCandleParser
{
    private static readonly string[] SplitDateTimeFormats =
    [
        "yyyy.MM.dd HH:mm",
        "yyyy.MM.dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd HH:mm:ss"
    ];

    /// <summary>
    /// Parses a single M1 OHLCV CSV row.
    /// Supports HistData semicolon rows, single timestamp CSV rows, and MetaTrader date/time split rows.
    /// </summary>
    public static OhlcvCandle? ParseLine(string line, string symbol)
    {
        string[] parts;
        DateTime timestamp;
        int priceStartIndex;

        if (line.Contains(';'))
        {
            // HistData format: 20150101 000000;1.21010;1.21020;1.21010;1.21020;0
            parts = line.Split(';');
            if (parts.Length < 5) return null;

            string dateStr = parts[0].Trim();
            if (dateStr.Length == 15) // "20150101 000000"
            {
                if (!DateTime.TryParseExact(dateStr, "yyyyMMdd HHmmss",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out timestamp))
                    return null;
            }
            else
            {
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out timestamp))
                    return null;
            }

            priceStartIndex = 1;
        }
        else
        {
            parts = line.Split(',');
            if (parts.Length < 5) return null;

            string firstPart = parts[0].Trim();
            string secondPart = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            if (parts.Length >= 6
                && TimeOnly.TryParse(secondPart, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                if (!DateTime.TryParseExact($"{firstPart} {secondPart}", SplitDateTimeFormats,
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out timestamp))
                    return null;

                priceStartIndex = 2;
            }
            else
            {
                // Standard CSV: 2015-01-01 00:00:00,1.21010,1.21020,1.21010,1.21020,0
                if (!DateTime.TryParse(firstPart, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out timestamp))
                    return null;

                priceStartIndex = 1;
            }
        }

        if (parts.Length <= priceStartIndex + 3)
            return null;

        if (!decimal.TryParse(parts[priceStartIndex].Trim(), CultureInfo.InvariantCulture, out decimal open)
            || !decimal.TryParse(parts[priceStartIndex + 1].Trim(), CultureInfo.InvariantCulture, out decimal high)
            || !decimal.TryParse(parts[priceStartIndex + 2].Trim(), CultureInfo.InvariantCulture, out decimal low)
            || !decimal.TryParse(parts[priceStartIndex + 3].Trim(), CultureInfo.InvariantCulture, out decimal close))
        {
            return null;
        }

        decimal volume = parts.Length > priceStartIndex + 4
            ? decimal.TryParse(parts[priceStartIndex + 4].Trim(), CultureInfo.InvariantCulture, out decimal v) ? v : 0m
            : 0m;

        return new OhlcvCandle
        {
            Id = 0,
            Asset = symbol,
            Timeframe = Timeframe.M1,
            Timestamp = timestamp.ToUniversalTime(),
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume
        };
    }
}
