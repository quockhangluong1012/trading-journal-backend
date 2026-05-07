using Moq;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.AiInsights.Domain;
using TradingJournal.Modules.AiInsights.Events;
using TradingJournal.Modules.AiInsights.Features.V1.Review;
using TradingJournal.Modules.AiInsights.Infrastructure;
using TradingJournal.Shared.Abstractions;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.Ai;

public sealed class GenerateReviewSummaryHandlerTests
{
    private readonly Mock<IAiInsightsDbContext> _context = new();
    private readonly Mock<IEventBus> _eventBus = new();
    private readonly Mock<IAiTradeDataProvider> _tradeDataProvider = new();

    [Fact]
    public async Task Handle_WhenCreatingNewReview_PersistsRuleBreaksFromSnapshot()
    {
        ReviewSnapshot snapshot = CreateSnapshot(ruleBreakTrades: 2);
        var reviewSet = DbSetMockHelper.CreateMockDbSet(new List<TradingReview>().AsQueryable());

        _context.Setup(x => x.TradingReviews).Returns(reviewSet.Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tradeDataProvider
            .Setup(x => x.BuildReviewSnapshotAsync(ReviewPeriodType.Weekly, snapshot.PeriodStart, 14, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var handler = new GenerateReviewSummary.Handler(_context.Object, _eventBus.Object, _tradeDataProvider.Object);

        Result<bool> result = await handler.Handle(
            new GenerateReviewSummary.Request(ReviewPeriodType.Weekly, snapshot.PeriodStart, snapshot.PeriodEnd, 14),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _context.Verify(x => x.TradingReviews.AddAsync(
            It.Is<TradingReview>(review =>
                review.PeriodStart == snapshot.PeriodStart &&
                review.PeriodEnd == snapshot.PeriodEnd &&
                review.RuleBreaks == snapshot.Metrics.RuleBreakTrades &&
                review.AiSummaryGenerating),
            It.IsAny<CancellationToken>()), Times.Once);
        _eventBus.Verify(x => x.PublishAsync(
            It.Is<GenerateReviewSummaryEvent>(notification =>
                notification.UserId == 14 &&
                notification.RuleBreaks == snapshot.Metrics.RuleBreakTrades),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ReviewSnapshot CreateSnapshot(int ruleBreakTrades)
    {
        return new ReviewSnapshot(
            ReviewPeriodType.Weekly,
            new DateTime(2026, 5, 5),
            new DateTime(2026, 5, 11).AddTicks(-1),
            new ReviewSnapshotMetrics(
                TotalTrades: 5,
                Wins: 3,
                Losses: 2,
                TotalPnl: 245.5m,
                WinRate: 60m,
                AverageWin: 110m,
                AverageLoss: -42.25m,
                BestTradePnl: 180m,
                WorstTradePnl: -55m,
                BestDayPnl: 140m,
                WorstDayPnl: -55m,
                LongTrades: 3,
                ShortTrades: 2,
                RuleBreakTrades: ruleBreakTrades,
                HighConfidenceTrades: 2,
                TopAsset: "EURUSD",
                PrimaryTradingZone: "London",
                DominantEmotion: "Focused",
                TopTechnicalTheme: "Liquidity Sweep"),
            [],
            []);
    }
}

public sealed class SaveReviewHandlerTests
{
    private readonly Mock<IAiInsightsDbContext> _context = new();
    private readonly Mock<IAiTradeDataProvider> _tradeDataProvider = new();

    [Fact]
    public async Task Handle_WhenCreatingNewReview_PersistsRuleBreaksFromSnapshot()
    {
        ReviewSnapshot snapshot = CreateSnapshot(ruleBreakTrades: 3);
        var reviewSet = DbSetMockHelper.CreateMockDbSet(new List<TradingReview>().AsQueryable());

        _context.Setup(x => x.TradingReviews).Returns(reviewSet.Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tradeDataProvider
            .Setup(x => x.BuildReviewSnapshotAsync(ReviewPeriodType.Weekly, snapshot.PeriodStart, 14, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var handler = new SaveReview.Handler(_context.Object, _tradeDataProvider.Object);

        Result<int> result = await handler.Handle(
            new SaveReview.Request(ReviewPeriodType.Weekly, snapshot.PeriodStart, snapshot.PeriodEnd, "review notes", 14),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _context.Verify(x => x.TradingReviews.AddAsync(
            It.Is<TradingReview>(review =>
                review.UserNotes == "review notes" &&
                review.RuleBreaks == snapshot.Metrics.RuleBreakTrades),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ReviewSnapshot CreateSnapshot(int ruleBreakTrades)
    {
        return new ReviewSnapshot(
            ReviewPeriodType.Weekly,
            new DateTime(2026, 5, 5),
            new DateTime(2026, 5, 11).AddTicks(-1),
            new ReviewSnapshotMetrics(
                TotalTrades: 6,
                Wins: 4,
                Losses: 2,
                TotalPnl: 310m,
                WinRate: 66.7m,
                AverageWin: 105m,
                AverageLoss: -55m,
                BestTradePnl: 190m,
                WorstTradePnl: -60m,
                BestDayPnl: 220m,
                WorstDayPnl: -60m,
                LongTrades: 4,
                ShortTrades: 2,
                RuleBreakTrades: ruleBreakTrades,
                HighConfidenceTrades: 3,
                TopAsset: "XAUUSD",
                PrimaryTradingZone: "New York",
                DominantEmotion: "Calm",
                TopTechnicalTheme: "Order Block"),
            [],
            []);
    }
}