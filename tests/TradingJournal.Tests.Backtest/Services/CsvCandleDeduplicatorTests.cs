using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Services;

namespace TradingJournal.Tests.Backtest.Services;

public sealed class CsvCandleDeduplicatorTests
{
    [Fact]
    public void KeepOnlyNewCandles_SkipsExistingAndDuplicateTimestamps()
    {
        DateTime existingTimestamp = new(2025, 10, 26, 19, 0, 0, DateTimeKind.Utc);
        DateTime newTimestamp = existingTimestamp.AddMinutes(1);

        List<OhlcvCandle> parsedCandles =
        [
            CreateCandle(existingTimestamp, 1.1m),
            CreateCandle(newTimestamp, 1.2m),
            CreateCandle(newTimestamp, 1.3m)
        ];

        List<OhlcvCandle> newCandles = CsvCandleDeduplicator.KeepOnlyNewCandles(
            parsedCandles,
            new HashSet<DateTime> { existingTimestamp });

        Assert.Single(newCandles);
        Assert.Equal(newTimestamp, newCandles[0].Timestamp);
        Assert.Equal(1.2m, newCandles[0].Open);
    }

    private static OhlcvCandle CreateCandle(DateTime timestamp, decimal open)
    {
        return new OhlcvCandle
        {
            Asset = "EURUSD",
            Timeframe = Timeframe.M1,
            Timestamp = timestamp,
            Open = open,
            High = open,
            Low = open,
            Close = open,
            Volume = 0m
        };
    }
}
