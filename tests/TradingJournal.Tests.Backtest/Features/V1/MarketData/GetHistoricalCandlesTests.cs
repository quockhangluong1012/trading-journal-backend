using Moq;
using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Features.V1.MarketData;
using TradingJournal.Modules.Backtest.Infrastructure;
using TradingJournal.Modules.Backtest.Services;
using TradingJournal.Tests.Backtest.Helpers;

namespace TradingJournal.Tests.Backtest.Features.V1.MarketData;

public sealed class GetHistoricalCandlesHandlerTests
{
    private readonly Mock<IBacktestDbContext> _context = new();
    private readonly Mock<ICandleAggregationService> _aggregationService = new();
    private readonly GetHistoricalCandles.Handler _handler;

    public GetHistoricalCandlesHandlerTests()
    {
        _handler = new GetHistoricalCandles.Handler(_context.Object, _aggregationService.Object);
    }

    [Fact]
    public async Task Handle_LoadsReferenceCandlesFromSevenDaysBeforeSessionStart()
    {
        DateTime sessionStart = new(2024, 1, 10, 9, 0, 0, DateTimeKind.Utc);
        DateTime currentTimestamp = sessionStart;
        DateTime expectedReferenceStart = sessionStart.AddDays(-7);
        BacktestSession session = new()
        {
            Id = 42,
            CreatedBy = 7,
            Asset = "EURUSD",
            StartDate = sessionStart,
            EndDate = sessionStart.AddDays(10),
            InitialBalance = 10_000m,
            CurrentBalance = 10_000m,
            Status = BacktestSessionStatus.InProgress,
            CurrentTimestamp = currentTimestamp,
            ActiveTimeframe = Timeframe.M15,
            PlaybackSpeed = 1,
            Leverage = 50,
            MaintenanceMarginPercentage = 0.50m,
            IsDataReady = true
        };

        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession> { session }.AsQueryable()).Object);

        _aggregationService
            .Setup(x => x.AggregatePagedAsync(
                It.IsAny<string>(),
                It.IsAny<Timeframe>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _handler.Handle(
            new GetHistoricalCandles.Request(session.Id, "M15", 1, 500) { UserId = session.CreatedBy },
            CancellationToken.None);

        _aggregationService.Verify(x => x.AggregatePagedAsync(
            "EURUSD",
            Timeframe.M15,
            expectedReferenceStart,
            currentTimestamp,
            1,
            500,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
