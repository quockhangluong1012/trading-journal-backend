using Microsoft.Extensions.Logging.Abstractions;
using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Services;

namespace TradingJournal.Tests.Backtest.Services;

/// <summary>
/// Comprehensive test suite for the OrderMatchingEngine.
///
/// Tests cover three critical simulation features:
///   1. Bid/Ask Simulation (Phantom Spread)
///   2. Price Gap & Slippage Handling
///   3. Intra-bar Collision (SchrÃ¶dinger's Candle)
///
/// Convention:
///   - All candle OHLC values represent BID prices
///   - ASK = BID + spread
///   - Long opens at ASK, closes at BID
///   - Short opens at BID, closes at ASK
/// </summary>
public class OrderMatchingEngineTests
{
    private OrderMatchingEngine _engine = null!;
    private static readonly DateTime TestTime = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    public OrderMatchingEngineTests()
    {
        _engine = new OrderMatchingEngine(NullLogger<OrderMatchingEngine>.Instance);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  HELPERS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private static OhlcvCandle MakeCandle(
        decimal open, decimal high, decimal low, decimal close,
        decimal volume = 1000m, DateTime? timestamp = null)
    {
        return new OhlcvCandle
        {
            Id = 1,
            Asset = "EUR/USD",
            Timeframe = Timeframe.M1,
            Timestamp = timestamp ?? TestTime,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume
        };
    }

    private static BacktestOrder MakeActivePosition(
        BacktestOrderSide side,
        decimal filledPrice,
        decimal positionSize = 100_000m,
        decimal? sl = null,
        decimal? tp = null,
        int id = 1)
    {
        return new BacktestOrder
        {
            Id = id,
            SessionId = 1,
            OrderType = BacktestOrderType.Market,
            Side = side,
            Status = BacktestOrderStatus.Active,
            EntryPrice = filledPrice,
            FilledPrice = filledPrice,
            PositionSize = positionSize,
            StopLoss = sl,
            TakeProfit = tp,
            OrderedAt = TestTime.AddMinutes(-10),
            FilledAt = TestTime.AddMinutes(-10)
        };
    }

    private static BacktestOrder MakePendingLimit(
        BacktestOrderSide side,
        decimal entryPrice,
        decimal positionSize = 100_000m,
        decimal? sl = null,
        decimal? tp = null,
        int id = 1)
    {
        return new BacktestOrder
        {
            Id = id,
            SessionId = 1,
            OrderType = BacktestOrderType.Limit,
            Side = side,
            Status = BacktestOrderStatus.Pending,
            EntryPrice = entryPrice,
            PositionSize = positionSize,
            StopLoss = sl,
            TakeProfit = tp,
            OrderedAt = TestTime.AddMinutes(-10)
        };
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FEATURE 1: BID/ASK SIMULATION (Phantom Spread)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Long_SL_Hit_At_Bid_Price()
    {
        // Arrange: Long position, SL at 1.1000
        // Bid Low = 1.0990 (touches SL), Ask Low = 1.0990 + 0.0002 = 1.0992
        // The SL should be checked against BID Low (the close price for longs)
        decimal spread = 0.0002m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 1.1050m, sl: 1.1000m, tp: 1.1200m);

        OhlcvCandle candle = MakeCandle(open: 1.1040m, high: 1.1060m, low: 1.0990m, close: 1.1020m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(1.1000m, result.Closes[0].ExitPrice); // Exits at SL level (Bid)
        Assert.Equal("SL Hit", result.Closes[0].Reason);
    }

    [Fact]
    public void Long_TP_Hit_At_Bid_Price()
    {
        // Arrange: Long TP at 1.1100
        // Bid High = 1.1120 (reaches TP), but exit at TP level (1.1100)
        decimal spread = 0.0002m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 1.1050m, sl: 1.0900m, tp: 1.1100m);

        OhlcvCandle candle = MakeCandle(open: 1.1060m, high: 1.1120m, low: 1.1040m, close: 1.1090m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(1.1100m, result.Closes[0].ExitPrice); // Exits at TP level (Bid)
        Assert.Equal("TP Hit", result.Closes[0].Reason);
    }

    [Fact]
    public void Short_SL_Hit_At_Ask_Price()
    {
        // Arrange: Short position, SL at 1.1100
        // Bid High = 1.1090, Ask High = 1.1090 + 0.0002 = 1.1092
        // The SL should be checked against ASK High. Ask High = 1.1092 < SL 1.1100 â†’ NO HIT
        // Now with Bid High = 1.1099, Ask High = 1.1101 â†’ HIT
        decimal spread = 0.0002m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 1.1050m, sl: 1.1100m, tp: 1.0900m);

        OhlcvCandle candle = MakeCandle(open: 1.1060m, high: 1.1099m, low: 1.1040m, close: 1.1070m);

        // Act: Ask High = 1.1099 + 0.0002 = 1.1101 >= SL 1.1100 â†’ HIT
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(1.1100m, result.Closes[0].ExitPrice); // Exits at SL level (Ask)
        Assert.Equal("SL Hit", result.Closes[0].Reason);
    }

    [Fact]
    public void Short_SL_NOT_Hit_When_Ask_High_Below_SL()
    {
        // Arrange: Short position, SL at 1.1100
        // Bid High = 1.1090, Ask High = 1.1090 + 0.0002 = 1.1092 < SL 1.1100
        // SL should NOT trigger because Ask High doesn't reach SL
        decimal spread = 0.0002m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 1.1050m, sl: 1.1100m, tp: 1.0900m);

        OhlcvCandle candle = MakeCandle(open: 1.1060m, high: 1.1090m, low: 1.1040m, close: 1.1070m);

        // Act: Ask High = 1.1090 + 0.0002 = 1.1092 < SL 1.1100 â†’ NO HIT
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Empty(result.Closes);
    }

    [Fact]
    public void Short_TP_Hit_At_Ask_Price()
    {
        // Arrange: Short TP at 1.1000
        // Short closes at ASK. Ask Low = Bid Low + spread.
        // Bid Low = 1.0990, Ask Low = 1.0990 + 0.0002 = 1.0992 â†’ Ask Low <= TP 1.1000 â†’ HIT
        decimal spread = 0.0002m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 1.1050m, sl: 1.1200m, tp: 1.1000m);

        OhlcvCandle candle = MakeCandle(open: 1.1040m, high: 1.1060m, low: 1.0990m, close: 1.1020m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(1.1000m, result.Closes[0].ExitPrice); // Exits at TP level
        Assert.Equal("TP Hit", result.Closes[0].Reason);
    }

    [Fact]
    public void Long_Entry_Limit_At_Ask_Price()
    {
        // Arrange: Buy limit at 1.1000 (ASK price the trader wants)
        // Ask Low = Bid Low + spread. Bid Low = 1.0990, spread = 0.0002
        // Ask Low = 1.0992 <= 1.1000 â†’ triggers. Fill at 1.1000.
        decimal spread = 0.0002m;
        BacktestOrder limit = MakePendingLimit(BacktestOrderSide.Long, entryPrice: 1.1000m);

        OhlcvCandle candle = MakeCandle(open: 1.1040m, high: 1.1060m, low: 1.0990m, close: 1.1020m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [limit], [], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Fills.Count);
        Assert.Equal(1.1000m, result.Fills[0].FilledPrice); // Filled at the ASK entry price
    }

    [Fact]
    public void Long_Limit_Not_Triggered_When_Ask_Low_Above_Entry()
    {
        // Arrange: Buy limit at 1.0980 (ASK price)
        // Bid Low = 1.0990, Ask Low = 1.0990 + 0.0010 = 1.1000 > 1.0980? No, 1.1000 > 0.0980 YES
        // Wait: Ask Low = 1.0990 + 0.0010 = 1.1000. 1.1000 > 1.0980 â†’ Ask never dipped to entry â†’ NO FILL
        decimal spread = 0.0010m;
        BacktestOrder limit = MakePendingLimit(BacktestOrderSide.Long, entryPrice: 1.0980m);

        OhlcvCandle candle = MakeCandle(open: 1.1040m, high: 1.1060m, low: 1.0990m, close: 1.1020m);

        // Act: Ask Low = 1.0990 + 0.0010 = 1.1000 > 1.0980 â†’ NO FILL
        MatchingResult result = _engine.EvaluateCandle(candle, [limit], [], 10000m, spread);

        // Assert
        Assert.Empty(result.Fills);
    }

    [Fact]
    public void Short_Entry_Limit_At_Bid_Price()
    {
        // Arrange: Sell limit at 1.1100 (BID price the trader wants)
        // Bid High = 1.1120 >= 1.1100 â†’ triggers. Fill at 1.1100.
        decimal spread = 0.0002m;
        BacktestOrder limit = MakePendingLimit(BacktestOrderSide.Short, entryPrice: 1.1100m);

        OhlcvCandle candle = MakeCandle(open: 1.1040m, high: 1.1120m, low: 1.1020m, close: 1.1050m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [limit], [], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Fills.Count);
        Assert.Equal(1.1100m, result.Fills[0].FilledPrice); // Filled at the BID entry price
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FEATURE 2: PRICE GAP & SLIPPAGE
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Gap_Down_Past_Long_SL_Executes_At_Open()
    {
        // Arrange: Long SL at 100. New bar opens at 95 (gapped down past SL).
        // Should execute at Bid Open (95), NOT at SL (100). Negative slippage.
        decimal spread = 0.5m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 110m, sl: 100m, tp: 120m);

        // Bar that gaps down past SL. Bid Open = 95.
        OhlcvCandle candle = MakeCandle(open: 95m, high: 98m, low: 93m, close: 96m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(95m, result.Closes[0].ExitPrice); // Bid Open, not SL level
        Assert.Contains("SL Hit", result.Closes[0].Reason);
        Assert.Contains("Gapped", result.Closes[0].Reason);
        Assert.Equal(-5m, result.Closes[0].Slippage); // 95 - 100 = -5 (negative slippage)
    }

    [Fact]
    public void Gap_Up_Past_Short_SL_Executes_At_AskOpen()
    {
        // Arrange: Short SL at 100. New bar opens at 105 (gapped up past SL).
        // Short closes at Ask. Ask Open = 105 + 0.5 = 105.5. Should execute at 105.5.
        decimal spread = 0.5m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 90m, sl: 100m, tp: 80m);

        OhlcvCandle candle = MakeCandle(open: 105m, high: 108m, low: 104m, close: 106m);

        // Act: Ask Open = 105 + 0.5 = 105.5 >= SL 100 â†’ gapped
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(105.5m, result.Closes[0].ExitPrice); // Ask Open
        Assert.Contains("SL Hit", result.Closes[0].Reason);
        Assert.Equal(5.5m, result.Closes[0].Slippage); // 105.5 - 100 = 5.5
    }

    [Fact]
    public void Gap_Up_Past_Long_TP_Beneficial_Slippage()
    {
        // Arrange: Long TP at 120. New bar opens at 125 (gapped up past TP).
        // Beneficial gap: executes at Bid Open (125), better than TP (120).
        decimal spread = 0.5m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 110m, sl: 100m, tp: 120m);

        OhlcvCandle candle = MakeCandle(open: 125m, high: 128m, low: 124m, close: 126m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(125m, result.Closes[0].ExitPrice); // Bid Open
        Assert.Contains("TP Hit", result.Closes[0].Reason);
        Assert.Contains("Gapped", result.Closes[0].Reason);
        Assert.Equal(5m, result.Closes[0].Slippage); // 125 - 120 = +5 (positive/beneficial)
    }

    [Fact]
    public void Gap_Down_Past_Short_TP_Beneficial_Slippage()
    {
        // Arrange: Short TP at 80. New bar opens at 75 (gapped down past TP).
        // Short closes at Ask. Ask Open = 75 + 0.5 = 75.5 <= TP 80 â†’ gapped past TP.
        decimal spread = 0.5m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 90m, sl: 100m, tp: 80m);

        OhlcvCandle candle = MakeCandle(open: 75m, high: 78m, low: 73m, close: 76m);

        // Act: Ask Open = 75.5 <= TP 80 â†’ beneficial gap
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(75.5m, result.Closes[0].ExitPrice); // Ask Open
        Assert.Contains("TP Hit", result.Closes[0].Reason);
    }

    [Fact]
    public void Limit_Order_Gap_Fill_At_Open_Price()
    {
        // Arrange: Buy limit at 100. Bar opens at 95 (gapped past entry).
        // Ask Open = 95 + 0.5 = 95.5 <= 100 â†’ fill at Ask Open (95.5), not at 100.
        decimal spread = 0.5m;
        BacktestOrder limit = MakePendingLimit(BacktestOrderSide.Long, entryPrice: 100m);

        OhlcvCandle candle = MakeCandle(open: 95m, high: 98m, low: 93m, close: 96m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [limit], [], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Fills.Count);
        Assert.Equal(95.5m, result.Fills[0].FilledPrice); // Ask Open (beneficial gap)
    }

    [Fact]
    public void Sell_Limit_Gap_Fill_At_Open_Price()
    {
        // Arrange: Sell limit at 100. Bar opens at 105 (gapped past entry).
        // Bid Open = 105 >= 100 â†’ fill at Bid Open (105), not at 100.
        decimal spread = 0.5m;
        BacktestOrder limit = MakePendingLimit(BacktestOrderSide.Short, entryPrice: 100m);

        OhlcvCandle candle = MakeCandle(open: 105m, high: 108m, low: 104m, close: 106m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [limit], [], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Fills.Count);
        Assert.Equal(105m, result.Fills[0].FilledPrice); // Bid Open (beneficial gap)
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  FEATURE 3: INTRA-BAR COLLISION (SchrÃ¶dinger's Candle)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Bullish_Candle_Long_Collision_SL_Wins()
    {
        // Arrange: Long position with SL and TP both within candle range.
        // Bullish candle (Close >= Open): path = Open â†’ Low â†’ High â†’ Close
        // Low extreme is evaluated FIRST â†’ for Long, Low checks SL â†’ SL wins
        decimal spread = 0m; // Zero spread to isolate collision logic
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 100m, sl: 95m, tp: 110m);

        // Bullish candle that hits both SL (95) and TP (110)
        // Open=98, Low=94, High=112, Close=108 (Close >= Open â†’ bullish)
        OhlcvCandle candle = MakeCandle(open: 98m, high: 112m, low: 94m, close: 108m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert: SL should win because bullish path hits Low first
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal("SL Hit", result.Closes[0].Reason);
        Assert.Equal(95m, result.Closes[0].ExitPrice);
    }

    [Fact]
    public void Bearish_Candle_Long_Collision_TP_Wins()
    {
        // Arrange: Long position with SL and TP both within candle range.
        // Bearish candle (Open > Close): path = Open â†’ High â†’ Low â†’ Close
        // High extreme is evaluated FIRST â†’ for Long, High checks TP â†’ TP wins
        decimal spread = 0m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 100m, sl: 95m, tp: 110m);

        // Bearish candle that hits both SL and TP
        // Open=108, High=112, Low=94, Close=96 (Open > Close â†’ bearish)
        OhlcvCandle candle = MakeCandle(open: 108m, high: 112m, low: 94m, close: 96m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert: TP should win because bearish path hits High first
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal("TP Hit", result.Closes[0].Reason);
        Assert.Equal(110m, result.Closes[0].ExitPrice);
    }

    [Fact]
    public void Bullish_Candle_Short_Collision_TP_Wins()
    {
        // Arrange: Short position with SL and TP both within candle range.
        // Bullish candle: path = Open â†’ Low â†’ High â†’ Close
        // Low extreme is evaluated FIRST â†’ for Short, Low checks TP â†’ TP wins
        decimal spread = 0m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 100m, sl: 110m, tp: 95m);

        // Bullish candle that hits both SL (110) and TP (95)
        OhlcvCandle candle = MakeCandle(open: 98m, high: 112m, low: 94m, close: 108m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert: TP should win because bullish path hits Low first â†’ Short TP at Low
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal("TP Hit", result.Closes[0].Reason);
        Assert.Equal(95m, result.Closes[0].ExitPrice);
    }

    [Fact]
    public void Bearish_Candle_Short_Collision_SL_Wins()
    {
        // Arrange: Short position with SL and TP both within candle range.
        // Bearish candle: path = Open â†’ High â†’ Low â†’ Close
        // High extreme is evaluated FIRST â†’ for Short, High checks SL â†’ SL wins
        decimal spread = 0m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 100m, sl: 110m, tp: 95m);

        // Bearish candle that hits both
        OhlcvCandle candle = MakeCandle(open: 108m, high: 112m, low: 94m, close: 96m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert: SL should win because bearish path hits High first â†’ Short SL at High
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal("SL Hit", result.Closes[0].Reason);
        Assert.Equal(110m, result.Closes[0].ExitPrice);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //  COMBINED FEATURES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public void Collision_With_Spread_Bullish_Short()
    {
        // Arrange: Short with spread. Bullish candle.
        // Short SL checked against Ask High, Short TP checked against Ask Low.
        // Bullish: Low first â†’ Short TP first.
        decimal spread = 0.5m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 100m, sl: 110m, tp: 92m);

        // Bullish candle: Bid Low=90, Ask Low=90.5 â†’ 90.5 <= TP 92 â†’ TP HIT
        // Bid High=112, Ask High=112.5 â†’ 112.5 >= SL 110 â†’ SL HIT (but evaluated second)
        OhlcvCandle candle = MakeCandle(open: 98m, high: 112m, low: 90m, close: 108m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert: TP wins (bullish, Low first, Short TP at Low)
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal("TP Hit", result.Closes[0].Reason);
        Assert.Equal(92m, result.Closes[0].ExitPrice);
    }

    [Fact]
    public void Gap_Takes_Priority_Over_Collision()
    {
        // Arrange: Long SL at 100. Bar opens at 90 (gapped below SL AND TP is 80... wait, TP is above).
        // Long SL at 100, TP at 120. Bar opens at 90 â†’ gapped past SL â†’ SL at Open price.
        // Even though TP at 120 might be hit by the High, gap check runs FIRST.
        decimal spread = 0m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 110m, sl: 100m, tp: 120m);

        // Bar gaps down and then rallies to hit TP too
        OhlcvCandle candle = MakeCandle(open: 90m, high: 125m, low: 88m, close: 122m);

        // Act: Open=90 <= SL=100 â†’ gap SL triggers FIRST
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert: SL (gapped) wins, not TP
        Assert.Equal(1, result.Closes.Count);
        Assert.Contains("SL Hit", result.Closes[0].Reason);
        Assert.Equal(90m, result.Closes[0].ExitPrice);
    }

    [Fact]
    public void No_Spread_Backward_Compatible()
    {
        // Arrange: With spread=0, the engine should produce the same results as the old engine
        // for basic SL/TP scenarios.
        decimal spread = 0m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 100m, sl: 95m, tp: 110m);

        // Normal candle that hits SL
        OhlcvCandle candle = MakeCandle(open: 99m, high: 101m, low: 94m, close: 97m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(95m, result.Closes[0].ExitPrice);
        Assert.Equal("SL Hit", result.Closes[0].Reason);
        decimal expectedPnl = (95m - 100m) * 100_000m; // -500,000
        Assert.Equal(expectedPnl, result.Closes[0].Pnl);
    }

    [Fact]
    public void Unrealized_PnL_Uses_Correct_Bid_Ask()
    {
        // Arrange: Both long and short positions, no SL/TP hit.
        // Long unrealized PnL uses Bid Close.
        // Short unrealized PnL uses Ask Close.
        decimal spread = 1.0m;

        BacktestOrder longPos = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 100m, id: 1);
        BacktestOrder shortPos = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 100m, id: 2);

        OhlcvCandle candle = MakeCandle(open: 101m, high: 103m, low: 99m, close: 102m);

        // Act â€” Use large balance to avoid triggering liquidation
        MatchingResult result = _engine.EvaluateCandle(candle, [], [longPos, shortPos], 1_000_000m, spread);

        // Assert: No closes
        Assert.Empty(result.Closes);

        // Long unrealized: (Bid Close - entry) * size = (102 - 100) * 100000 = 200000
        // Short unrealized: (entry - Ask Close) * size = (100 - 103) * 100000 = -300000
        // Total: 200000 + (-300000) = -100000
        Assert.Equal(-100_000m, result.UnrealizedPnl);
    }

    [Fact]
    public void Liquidation_Uses_Bid_Ask_For_Exit()
    {
        // Arrange: Position with tiny balance that triggers liquidation
        decimal spread = 1.0m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 100m, positionSize: 100m);

        // Big move up: Short is losing. Ask Close = 200 + 1 = 201
        // Unrealized PnL = (100 - 201) * 100 = -10100
        OhlcvCandle candle = MakeCandle(open: 180m, high: 210m, low: 175m, close: 200m);

        // Act: Balance = 50, Equity = 50 + (-10100) = -10050 â†’ LIQUIDATED
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 50m, spread);

        // Assert
        Assert.True(result.IsLiquidated);
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(201m, result.Closes[0].ExitPrice); // Ask Close for Short liquidation
        Assert.Equal("Liquidated", result.Closes[0].Reason);
    }

    [Fact]
    public void Doji_Candle_Treats_As_Bullish()
    {
        // Arrange: Doji candle (Close == Open) is treated as bullish.
        // Path: Open â†’ Low â†’ High â†’ Close
        // For Long: Low checks SL first.
        decimal spread = 0m;
        BacktestOrder position = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 100m, sl: 95m, tp: 110m);

        // Doji: Open=100, Close=100 (Close >= Open â†’ bullish)
        OhlcvCandle candle = MakeCandle(open: 100m, high: 112m, low: 94m, close: 100m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [position], 10000m, spread);

        // Assert: SL wins (bullish â†’ Low first â†’ Long SL)
        Assert.Equal(1, result.Closes.Count);
        Assert.Equal("SL Hit", result.Closes[0].Reason);
    }

    [Fact]
    public void Multiple_Positions_Independent_Evaluation()
    {
        // Arrange: Two positions, one hits SL, one hits TP
        decimal spread = 0m;

        BacktestOrder longSL = MakeActivePosition(
            BacktestOrderSide.Long, filledPrice: 100m, sl: 95m, tp: 120m, id: 1);
        BacktestOrder shortTP = MakeActivePosition(
            BacktestOrderSide.Short, filledPrice: 100m, sl: 120m, tp: 95m, id: 2);

        // Candle that drops to 94 (hits Long SL and Short TP)
        OhlcvCandle candle = MakeCandle(open: 99m, high: 101m, low: 94m, close: 97m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [], [longSL, shortTP], 10000m, spread);

        // Assert: Both should close
        Assert.Equal(2, result.Closes.Count);
        Assert.True(result.Closes.Any(c => c.OrderId == 1 && c.Reason == "SL Hit"));
        Assert.True(result.Closes.Any(c => c.OrderId == 2 && c.Reason == "TP Hit"));
    }

    [Fact]
    public void Limit_Fill_Then_SL_Hit_Same_Candle()
    {
        // Arrange: Limit order fills, then the same volatile candle hits SL
        decimal spread = 0m;
        BacktestOrder limit = MakePendingLimit(
            BacktestOrderSide.Long, entryPrice: 98m, sl: 93m, tp: 110m, id: 1);

        // Volatile bearish candle: fills the limit at 98, then hits SL at 93
        // Bearish: Open â†’ High â†’ Low â†’ Close
        // High first â†’ Long TP check (110 not reached) â†’ Low second â†’ Long SL check (92 <= 93 â†’ HIT)
        OhlcvCandle candle = MakeCandle(open: 99m, high: 101m, low: 92m, close: 94m);

        // Act
        MatchingResult result = _engine.EvaluateCandle(candle, [limit], [], 10000m, spread);

        // Assert
        Assert.Equal(1, result.Fills.Count);
        Assert.Equal(98m, result.Fills[0].FilledPrice);

        Assert.Equal(1, result.Closes.Count);
        Assert.Equal(93m, result.Closes[0].ExitPrice);
        Assert.Equal("SL Hit", result.Closes[0].Reason);
    }
}
