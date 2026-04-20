using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Modules.Trades.Features.V1.Review;
using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Review;

public sealed class ReviewPeriodCalculatorTests
{
    [Fact]
    public void GetBounds_Normalizes_Weekly_Period_From_Sunday_To_Monday_Start()
    {
        ReviewPeriodBounds bounds = ReviewPeriodCalculator.GetBounds(
            ReviewPeriodType.Weekly,
            new DateTime(2026, 4, 19));

        Assert.Equal(new DateTime(2026, 4, 13), bounds.Start);
        Assert.Equal(new DateTime(2026, 4, 20).AddTicks(-1), bounds.End);
    }

    [Fact]
    public void GetBounds_Normalizes_Quarterly_Period_To_Quarter_Edges()
    {
        ReviewPeriodBounds bounds = ReviewPeriodCalculator.GetBounds(
            ReviewPeriodType.Quarterly,
            new DateTime(2026, 5, 22));

        Assert.Equal(new DateTime(2026, 4, 1), bounds.Start);
        Assert.Equal(new DateTime(2026, 7, 1).AddTicks(-1), bounds.End);
    }
}

public sealed class ReviewSnapshotMetricsTests
{
    [Fact]
    public void FromTrades_Calculates_Performance_And_Theme_Signals()
    {
        IReadOnlyCollection<ReviewTradeInsight> trades =
        [
            new ReviewTradeInsight(
                TradeId: 1,
                Asset: "EURUSD",
                Position: PositionType.Long,
                Pnl: 320,
                OpenDate: new DateTime(2026, 4, 14, 8, 0, 0),
                ClosedDate: new DateTime(2026, 4, 14, 10, 30, 0),
                EntryPrice: 1.1035,
                ExitPrice: 1.1085,
                IsRuleBroken: false,
                RuleBreakReason: null,
                TradingZone: "London",
                ConfidenceLevel: ConfidenceLevel.VeryHigh,
                TechnicalThemes: ["Order Block", "FVG"],
                EmotionTags: ["Focused"],
                ChecklistItems: ["Wait for sweep"],
                Notes: "Waited for confirmation and scaled out cleanly."),
            new ReviewTradeInsight(
                TradeId: 2,
                Asset: "EURUSD",
                Position: PositionType.Short,
                Pnl: -140,
                OpenDate: new DateTime(2026, 4, 16, 13, 0, 0),
                ClosedDate: new DateTime(2026, 4, 16, 15, 0, 0),
                EntryPrice: 1.1090,
                ExitPrice: 1.1110,
                IsRuleBroken: true,
                RuleBreakReason: "Chased after news impulse.",
                TradingZone: "New York",
                ConfidenceLevel: ConfidenceLevel.Low,
                TechnicalThemes: ["Liquidity Sweep"],
                EmotionTags: ["Frustrated"],
                ChecklistItems: ["Wait for pullback"],
                Notes: "Forced re-entry after missing the first move."),
            new ReviewTradeInsight(
                TradeId: 3,
                Asset: "XAUUSD",
                Position: PositionType.Long,
                Pnl: 80,
                OpenDate: new DateTime(2026, 4, 17, 7, 45, 0),
                ClosedDate: new DateTime(2026, 4, 17, 9, 0, 0),
                EntryPrice: 3331,
                ExitPrice: 3339,
                IsRuleBroken: false,
                RuleBreakReason: null,
                TradingZone: "London",
                ConfidenceLevel: ConfidenceLevel.Hight,
                TechnicalThemes: ["Order Block"],
                EmotionTags: ["Focused", "Calm"],
                ChecklistItems: ["Wait for sweep"],
                Notes: "Took partials at first target and left runner."),
        ];

        ReviewSnapshotMetrics metrics = ReviewSnapshotMetrics.FromTrades(trades);

        Assert.Equal(3, metrics.TotalTrades);
        Assert.Equal(2, metrics.Wins);
        Assert.Equal(1, metrics.Losses);
        Assert.Equal(260, metrics.TotalPnl);
        Assert.Equal(66.7, metrics.WinRate);
        Assert.Equal(200, metrics.AverageWin);
        Assert.Equal(-140, metrics.AverageLoss);
        Assert.Equal(320, metrics.BestTradePnl);
        Assert.Equal(-140, metrics.WorstTradePnl);
        Assert.Equal(320, metrics.BestDayPnl);
        Assert.Equal(-140, metrics.WorstDayPnl);
        Assert.Equal(2, metrics.LongTrades);
        Assert.Equal(1, metrics.ShortTrades);
        Assert.Equal(1, metrics.RuleBreakTrades);
        Assert.Equal(2, metrics.HighConfidenceTrades);
        Assert.Equal("EURUSD", metrics.TopAsset);
        Assert.Equal("London", metrics.PrimaryTradingZone);
        Assert.Equal("Order Block", metrics.TopTechnicalTheme);
        Assert.Equal("Focused", metrics.DominantEmotion);
    }
}