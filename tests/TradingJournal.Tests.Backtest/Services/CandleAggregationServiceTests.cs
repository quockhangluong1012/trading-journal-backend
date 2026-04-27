using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Infrastructure;
using TradingJournal.Modules.Backtest.Services;
using TradingJournal.Tests.Backtest.Helpers;

namespace TradingJournal.Tests.Backtest.Services;

public sealed class CandleAggregationServiceTests
{
    private readonly Mock<IBacktestDbContext> _context = new();
    private readonly CandleAggregationService _service;

    public CandleAggregationServiceTests()
    {
        _service = new CandleAggregationService(
            _context.Object,
            NullLogger<CandleAggregationService>.Instance);
    }

    private List<OhlcvCandle> CreateM1Candles(string symbol, DateTime start, int count)
    {
        var candles = new List<OhlcvCandle>();
        for (int i = 0; i < count; i++)
        {
            candles.Add(new OhlcvCandle
            {
                Id = i + 1,
                Asset = symbol,
                Timeframe = Timeframe.M1,
                Timestamp = start.AddMinutes(i),
                Open = 100 + i,
                High = 101 + i,
                Low = 99 + i,
                Close = 100.5m + i,
                Volume = 1000 + i * 10
            });
        }
        return candles;
    }

    [Fact]
    public async Task AggregateAsync_M1_ReturnsRawCandles()
    {
        var start = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(4);
        var candles = CreateM1Candles("EURUSD", start, 5);

        _context.Setup(x => x.OhlcvCandles)
            .Returns(DbSetMockHelper.CreateMockDbSet(candles.AsQueryable()).Object);

        var result = await _service.AggregateAsync("EURUSD", Timeframe.M1, start, end);

        Assert.Equal(5, result.Count);
        Assert.All(result, c => Assert.Equal(Timeframe.M1, c.Timeframe));
    }

    [Fact]
    public async Task AggregateAsync_M5_AggregatesCorrectly()
    {
        var start = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(9);
        // 10 M1 candles → should produce 2 M5 candles
        var candles = CreateM1Candles("EURUSD", start, 10);

        _context.Setup(x => x.OhlcvCandles)
            .Returns(DbSetMockHelper.CreateMockDbSet(candles.AsQueryable()).Object);

        var result = await _service.AggregateAsync("EURUSD", Timeframe.M5, start, end);

        Assert.Equal(2, result.Count);
        // First M5 candle: Open from first M1, Close from 5th M1
        Assert.Equal(candles[0].Open, result[0].Open);
        Assert.Equal(candles[4].Close, result[0].Close);
        // Second M5 candle
        Assert.Equal(candles[5].Open, result[1].Open);
        Assert.Equal(candles[9].Close, result[1].Close);
    }

    [Fact]
    public async Task AggregateAsync_HighAndLow_CalculatedCorrectly()
    {
        var start = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var m1Candles = new List<OhlcvCandle>
        {
            new() { Id = 1, Asset = "EURUSD", Timeframe = Timeframe.M1, Timestamp = start,
                     Open = 100, High = 105, Low = 98, Close = 102, Volume = 100 },
            new() { Id = 2, Asset = "EURUSD", Timeframe = Timeframe.M1, Timestamp = start.AddMinutes(1),
                     Open = 102, High = 110, Low = 95, Close = 108, Volume = 200 },
            new() { Id = 3, Asset = "EURUSD", Timeframe = Timeframe.M1, Timestamp = start.AddMinutes(2),
                     Open = 108, High = 112, Low = 100, Close = 106, Volume = 150 },
            new() { Id = 4, Asset = "EURUSD", Timeframe = Timeframe.M1, Timestamp = start.AddMinutes(3),
                     Open = 106, High = 108, Low = 99, Close = 104, Volume = 120 },
            new() { Id = 5, Asset = "EURUSD", Timeframe = Timeframe.M1, Timestamp = start.AddMinutes(4),
                     Open = 104, High = 107, Low = 101, Close = 103, Volume = 130 },
        };

        _context.Setup(x => x.OhlcvCandles)
            .Returns(DbSetMockHelper.CreateMockDbSet(m1Candles.AsQueryable()).Object);

        var result = await _service.AggregateAsync("EURUSD", Timeframe.M5, start, start.AddMinutes(4));

        Assert.Single(result);
        Assert.Equal(100, result[0].Open);    // First candle's open
        Assert.Equal(112, result[0].High);    // Max high across all
        Assert.Equal(95, result[0].Low);      // Min low across all
        Assert.Equal(103, result[0].Close);   // Last candle's close
        Assert.Equal(700, result[0].Volume);  // Sum of all volumes
    }

    [Fact]
    public async Task AggregateAsync_EmptyData_ReturnsEmptyList()
    {
        var start = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(60);

        _context.Setup(x => x.OhlcvCandles)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<OhlcvCandle>().AsQueryable()).Object);

        var result = await _service.AggregateAsync("EURUSD", Timeframe.H1, start, end);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AggregateAsync_H1_AggregatesCorrectly()
    {
        var start = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        // 120 M1 candles → should produce 2 H1 candles
        var candles = CreateM1Candles("XAUUSD", start, 120);

        _context.Setup(x => x.OhlcvCandles)
            .Returns(DbSetMockHelper.CreateMockDbSet(candles.AsQueryable()).Object);

        var result = await _service.AggregateAsync("XAUUSD", Timeframe.H1, start, start.AddMinutes(119));

        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(Timeframe.H1, c.Timeframe));
        Assert.All(result, c => Assert.Equal("XAUUSD", c.Asset));
    }

    [Fact]
    public async Task AggregateAsync_SetsCorrectTimeframe()
    {
        var start = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var candles = CreateM1Candles("EURUSD", start, 15);

        _context.Setup(x => x.OhlcvCandles)
            .Returns(DbSetMockHelper.CreateMockDbSet(candles.AsQueryable()).Object);

        var result = await _service.AggregateAsync("EURUSD", Timeframe.M15, start, start.AddMinutes(14));

        Assert.Single(result);
        Assert.Equal(Timeframe.M15, result[0].Timeframe);
    }

    [Fact]
    public async Task GetNextAggregatedCandleAsync_M1_ReturnsNextRawCandle()
    {
        var start = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var candles = CreateM1Candles("EURUSD", start, 5);

        _context.Setup(x => x.OhlcvCandles)
            .Returns(DbSetMockHelper.CreateMockDbSet(candles.AsQueryable()).Object);

        var result = await _service.GetNextAggregatedCandleAsync("EURUSD", Timeframe.M1, start);

        Assert.NotNull(result);
        Assert.Equal(start.AddMinutes(1), result.Timestamp);
    }

    [Fact]
    public async Task GetNextAggregatedCandleAsync_ReturnsNull_WhenNoCandlesExist()
    {
        _context.Setup(x => x.OhlcvCandles)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<OhlcvCandle>().AsQueryable()).Object);

        var result = await _service.GetNextAggregatedCandleAsync(
            "EURUSD", Timeframe.M5, DateTime.UtcNow);

        Assert.Null(result);
    }
}
