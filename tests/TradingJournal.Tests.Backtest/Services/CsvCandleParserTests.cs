using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Services;

namespace TradingJournal.Tests.Backtest.Services;

public sealed class CsvCandleParserTests
{
    [Fact]
    public void ParseLine_ParsesMetaTraderSplitDateTimeFormat()
    {
        OhlcvCandle? candle = CsvCandleParser.ParseLine(
            "2026.04.01,00:00,1.156070,1.156070,1.155960,1.156020,0",
            "EURUSD");

        Assert.NotNull(candle);
        Assert.Equal("EURUSD", candle.Asset);
        Assert.Equal(Timeframe.M1, candle.Timeframe);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), candle.Timestamp);
        Assert.Equal(1.156070m, candle.Open);
        Assert.Equal(1.156070m, candle.High);
        Assert.Equal(1.155960m, candle.Low);
        Assert.Equal(1.156020m, candle.Close);
        Assert.Equal(0m, candle.Volume);
    }
}
