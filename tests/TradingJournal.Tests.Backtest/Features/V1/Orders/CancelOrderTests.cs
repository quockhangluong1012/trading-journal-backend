using Moq;
using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Features.V1.Orders;
using TradingJournal.Modules.Backtest.Infrastructure;
using TradingJournal.Tests.Backtest.Helpers;

namespace TradingJournal.Tests.Backtest.Features.V1.Orders;

public sealed class CancelOrderHandlerTests
{
    private readonly Mock<IBacktestDbContext> _context = new();
    private readonly CancelOrder.Handler _handler;

    public CancelOrderHandlerTests()
    {
        _handler = new CancelOrder.Handler(_context.Object);
    }

    [Fact]
    public async Task Handle_CancelsPendingOrder_Successfully()
    {
        var session = new BacktestSession { Id = 1, CreatedBy = 42 };
        var order = new BacktestOrder
        {
            Id = 5,
            SessionId = 1,
            Session = session,
            Status = BacktestOrderStatus.Pending,
            OrderType = BacktestOrderType.Limit,
            Side = BacktestOrderSide.Long,
            EntryPrice = 1.0800m,
            PositionSize = 1m
        };

        _context.Setup(x => x.BacktestOrders)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestOrder> { order }.AsQueryable()).Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(
            new CancelOrder.Request(5) { UserId = 42 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BacktestOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenOrderNotFound()
    {
        _context.Setup(x => x.BacktestOrders)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestOrder>().AsQueryable()).Object);

        var result = await _handler.Handle(
            new CancelOrder.Request(999) { UserId = 42 }, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenOrderIsActive()
    {
        var session = new BacktestSession { Id = 1, CreatedBy = 42 };
        var order = new BacktestOrder
        {
            Id = 5,
            SessionId = 1,
            Session = session,
            Status = BacktestOrderStatus.Active,
            OrderType = BacktestOrderType.Market,
            Side = BacktestOrderSide.Long,
            EntryPrice = 1.0850m,
            FilledPrice = 1.0850m,
            PositionSize = 1m
        };

        _context.Setup(x => x.BacktestOrders)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestOrder> { order }.AsQueryable()).Object);

        var result = await _handler.Handle(
            new CancelOrder.Request(5) { UserId = 42 }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BacktestOrderStatus.Active, order.Status);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenOrderIsClosed()
    {
        var session = new BacktestSession { Id = 1, CreatedBy = 42 };
        var order = new BacktestOrder
        {
            Id = 5,
            SessionId = 1,
            Session = session,
            Status = BacktestOrderStatus.Closed,
            OrderType = BacktestOrderType.Market,
            Side = BacktestOrderSide.Long,
            EntryPrice = 1.0850m,
            PositionSize = 1m
        };

        _context.Setup(x => x.BacktestOrders)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestOrder> { order }.AsQueryable()).Object);

        var result = await _handler.Handle(
            new CancelOrder.Request(5) { UserId = 42 }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BacktestOrderStatus.Closed, order.Status);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenWrongUser()
    {
        var session = new BacktestSession { Id = 1, CreatedBy = 42 };
        var order = new BacktestOrder
        {
            Id = 5,
            SessionId = 1,
            Session = session,
            Status = BacktestOrderStatus.Pending,
            OrderType = BacktestOrderType.Limit,
            Side = BacktestOrderSide.Long,
            EntryPrice = 1.0800m,
            PositionSize = 1m
        };

        _context.Setup(x => x.BacktestOrders)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestOrder> { order }.AsQueryable()).Object);

        var result = await _handler.Handle(
            new CancelOrder.Request(5) { UserId = 99 }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BacktestOrderStatus.Pending, order.Status);
    }
}
