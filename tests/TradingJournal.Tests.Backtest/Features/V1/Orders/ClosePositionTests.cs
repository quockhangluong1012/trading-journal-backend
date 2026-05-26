using Microsoft.EntityFrameworkCore;
using Moq;
using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Features.V1.Orders;
using TradingJournal.Modules.Backtest.Infrastructure;
using TradingJournal.Tests.Backtest.Helpers;

namespace TradingJournal.Tests.Backtest.Features.V1.Orders;

public sealed class ClosePositionHandlerTests
{
    private readonly Mock<IBacktestDbContext> _context = new();
    private readonly ClosePosition.Handler _handler;

    public ClosePositionHandlerTests()
    {
        _handler = new ClosePosition.Handler(_context.Object);
    }

    [Fact]
    public async Task Handle_UsesSimulatedSessionTimestamp_ForManualCloseMarkers()
    {
        DateTime simulatedTimestamp = new(2024, 2, 29, 17, 15, 0, DateTimeKind.Utc);

        BacktestSession session = new()
        {
            Id = 10,
            CreatedBy = 42,
            CurrentBalance = 10_000m,
            CurrentTimestamp = simulatedTimestamp,
            Status = BacktestSessionStatus.InProgress,
        };

        BacktestOrder order = new()
        {
            Id = 5,
            SessionId = session.Id,
            Session = session,
            OrderType = BacktestOrderType.Market,
            Side = BacktestOrderSide.Long,
            Status = BacktestOrderStatus.Active,
            EntryPrice = 100m,
            FilledPrice = 100m,
            PositionSize = 2m,
            OrderedAt = simulatedTimestamp.AddMinutes(-5),
            FilledAt = simulatedTimestamp.AddMinutes(-5),
        };

        List<BacktestOrder> orders = [order];
        List<BacktestTradeResult> tradeResults = [];

        Mock<DbSet<BacktestOrder>> orderDbSet = DbSetMockHelper.CreateMockDbSet(orders.AsQueryable());
        Mock<DbSet<BacktestTradeResult>> tradeResultDbSet = DbSetMockHelper.CreateMockDbSet(tradeResults.AsQueryable());
        tradeResultDbSet
            .Setup(dbSet => dbSet.Add(It.IsAny<BacktestTradeResult>()))
            .Callback<BacktestTradeResult>(tradeResults.Add);

        _context.Setup(context => context.BacktestOrders).Returns(orderDbSet.Object);
        _context.Setup(context => context.BacktestTradeResults).Returns(tradeResultDbSet.Object);
        _context.Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(new ClosePosition.Request(order.Id, 105m) { UserId = 42 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(simulatedTimestamp, order.ClosedAt);
        Assert.Single(tradeResults);
        Assert.Equal(simulatedTimestamp, tradeResults[0].ExitTime);
    }
}