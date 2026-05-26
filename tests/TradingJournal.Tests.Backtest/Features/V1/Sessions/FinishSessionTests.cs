using Microsoft.EntityFrameworkCore;
using Moq;
using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Features.V1.Sessions;
using TradingJournal.Modules.Backtest.Infrastructure;
using TradingJournal.Tests.Backtest.Helpers;

namespace TradingJournal.Tests.Backtest.Features.V1.Sessions;

public sealed class FinishSessionHandlerTests
{
    private readonly Mock<IBacktestDbContext> _context = new();
    private readonly FinishSession.Handler _handler;

    public FinishSessionHandlerTests()
    {
        _handler = new FinishSession.Handler(_context.Object);
    }

    private void SetupTransactionalContext(
        List<BacktestSession> sessions,
        List<BacktestOrder> orders,
        List<BacktestTradeResult> tradeResults)
    {
        Mock<DbSet<BacktestSession>> sessionDbSet = DbSetMockHelper.CreateMockDbSet(sessions.AsQueryable());
        Mock<DbSet<BacktestOrder>> orderDbSet = DbSetMockHelper.CreateMockDbSet(orders.AsQueryable());
        Mock<DbSet<BacktestTradeResult>> tradeResultDbSet = DbSetMockHelper.CreateMockDbSet(tradeResults.AsQueryable());
        tradeResultDbSet
            .Setup(dbSet => dbSet.Add(It.IsAny<BacktestTradeResult>()))
            .Callback<BacktestTradeResult>(tradeResults.Add);

        _context.Setup(context => context.BacktestSessions).Returns(sessionDbSet.Object);
        _context.Setup(context => context.BacktestOrders).Returns(orderDbSet.Object);
        _context.Setup(context => context.BacktestTradeResults).Returns(tradeResultDbSet.Object);
        _context.Setup(context => context.BeginTransaction()).Returns(Task.CompletedTask);
        _context.Setup(context => context.CommitTransaction()).Returns(Task.CompletedTask);
        _context.Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Handle_CompletesSessionAndSettlesOrders_WhenSessionIsInProgress()
    {
        BacktestSession session = new()
        {
            Id = 10,
            CreatedBy = 42,
            Asset = "EURUSD",
            InitialBalance = 10_000m,
            CurrentBalance = 10_000m,
            Spread = 0.5m,
            Status = BacktestSessionStatus.InProgress,
            CurrentTimestamp = new DateTime(2024, 2, 29, 17, 15, 0, DateTimeKind.Utc),
        };

        BacktestOrder pendingOrder = new()
        {
            Id = 1,
            SessionId = session.Id,
            OrderType = BacktestOrderType.Limit,
            Side = BacktestOrderSide.Long,
            Status = BacktestOrderStatus.Pending,
            EntryPrice = 1.0800m,
            PositionSize = 1m,
            OrderedAt = session.CurrentTimestamp,
        };

        BacktestOrder activeOrder = new()
        {
            Id = 2,
            SessionId = session.Id,
            OrderType = BacktestOrderType.Market,
            Side = BacktestOrderSide.Long,
            Status = BacktestOrderStatus.Active,
            EntryPrice = 100m,
            FilledPrice = 100m,
            PositionSize = 2m,
            OrderedAt = session.CurrentTimestamp.AddMinutes(-15),
            FilledAt = session.CurrentTimestamp.AddMinutes(-15),
        };

        List<BacktestSession> sessions = [session];
        List<BacktestOrder> orders = [pendingOrder, activeOrder];
        List<BacktestTradeResult> tradeResults = [];

        SetupTransactionalContext(sessions, orders, tradeResults);

        var result = await _handler.Handle(new FinishSession.Request(session.Id, 110m) { UserId = 42 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BacktestSessionStatus.Completed, session.Status);
        Assert.Equal(session.CurrentTimestamp, session.EndDate);
        Assert.Equal(BacktestOrderStatus.Cancelled, pendingOrder.Status);
        Assert.Equal(BacktestOrderStatus.Closed, activeOrder.Status);
        Assert.Equal(110m, activeOrder.ExitPrice);
        Assert.Equal(20m, activeOrder.Pnl);
        Assert.Equal(session.CurrentTimestamp, activeOrder.ClosedAt);
        Assert.Equal(10_020m, session.CurrentBalance);
        Assert.Single(tradeResults);
        Assert.Equal(110m, tradeResults[0].ExitPrice);
        Assert.Equal("Session Finished", tradeResults[0].ExitReason);
        Assert.Equal(10_020m, tradeResults[0].BalanceAfter);
    }

    [Fact]
    public async Task Handle_CancelsPendingOrders_WhenNoActivePositions()
    {
        BacktestSession session = new()
        {
            Id = 20,
            CreatedBy = 42,
            Asset = "XAUUSD",
            InitialBalance = 10_000m,
            CurrentBalance = 10_000m,
            Status = BacktestSessionStatus.InProgress,
            CurrentTimestamp = new DateTime(2024, 2, 25, 10, 0, 0, DateTimeKind.Utc),
        };

        BacktestOrder pendingOrder = new()
        {
            Id = 2,
            SessionId = session.Id,
            OrderType = BacktestOrderType.Limit,
            Side = BacktestOrderSide.Long,
            Status = BacktestOrderStatus.Pending,
            EntryPrice = 103m,
            PositionSize = 1m,
            OrderedAt = session.CurrentTimestamp.AddMinutes(-10),
        };

        List<BacktestSession> sessions = [session];
        List<BacktestOrder> orders = [pendingOrder];
        List<BacktestTradeResult> tradeResults = [];

        SetupTransactionalContext(sessions, orders, tradeResults);

        var result = await _handler.Handle(new FinishSession.Request(session.Id, null) { UserId = 42 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BacktestSessionStatus.Completed, session.Status);
        Assert.Equal(session.CurrentTimestamp, session.EndDate);
        Assert.Equal(BacktestOrderStatus.Cancelled, pendingOrder.Status);
        Assert.Empty(tradeResults);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenActiveOrdersExistButExitPriceIsMissing()
    {
        BacktestSession session = new()
        {
            Id = 21,
            CreatedBy = 42,
            Asset = "EURUSD",
            CurrentBalance = 10_000m,
            Status = BacktestSessionStatus.InProgress,
            CurrentTimestamp = new DateTime(2024, 2, 26, 9, 30, 0, DateTimeKind.Utc),
        };

        BacktestOrder activeOrder = new()
        {
            Id = 3,
            SessionId = session.Id,
            OrderType = BacktestOrderType.Market,
            Side = BacktestOrderSide.Long,
            Status = BacktestOrderStatus.Active,
            EntryPrice = 100m,
            FilledPrice = 100m,
            PositionSize = 1m,
            OrderedAt = session.CurrentTimestamp.AddMinutes(-5),
            FilledAt = session.CurrentTimestamp.AddMinutes(-5),
        };

        Mock<DbSet<BacktestSession>> sessionDbSet = DbSetMockHelper.CreateMockDbSet(new List<BacktestSession> { session }.AsQueryable());
        Mock<DbSet<BacktestOrder>> orderDbSet = DbSetMockHelper.CreateMockDbSet(new List<BacktestOrder> { activeOrder }.AsQueryable());

        _context.Setup(context => context.BacktestSessions).Returns(sessionDbSet.Object);
        _context.Setup(context => context.BacktestOrders).Returns(orderDbSet.Object);

        var result = await _handler.Handle(new FinishSession.Request(session.Id, null) { UserId = 42 }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BacktestOrderStatus.Active, activeOrder.Status);
        Assert.Equal(BacktestSessionStatus.InProgress, session.Status);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenSessionIsNotInProgress()
    {
        BacktestSession session = new()
        {
            Id = 22,
            CreatedBy = 42,
            Status = BacktestSessionStatus.Completed,
            CurrentTimestamp = new DateTime(2024, 2, 29, 17, 15, 0, DateTimeKind.Utc),
        };

        Mock<DbSet<BacktestSession>> sessionDbSet = DbSetMockHelper.CreateMockDbSet(new List<BacktestSession> { session }.AsQueryable());

        _context.Setup(context => context.BacktestSessions).Returns(sessionDbSet.Object);

        var result = await _handler.Handle(new FinishSession.Request(session.Id, 110m) { UserId = 42 }, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_AppliesSpread_WhenFinishingShortPositions()
    {
        BacktestSession session = new()
        {
            Id = 30,
            CreatedBy = 42,
            Asset = "EURUSD",
            InitialBalance = 10_000m,
            CurrentBalance = 10_000m,
            Spread = 0.5m,
            Status = BacktestSessionStatus.InProgress,
            CurrentTimestamp = new DateTime(2024, 2, 29, 17, 15, 0, DateTimeKind.Utc),
        };

        BacktestOrder activeOrder = new()
        {
            Id = 11,
            SessionId = session.Id,
            OrderType = BacktestOrderType.Market,
            Side = BacktestOrderSide.Short,
            Status = BacktestOrderStatus.Active,
            EntryPrice = 110m,
            FilledPrice = 110m,
            PositionSize = 1m,
            OrderedAt = session.CurrentTimestamp.AddMinutes(-5),
            FilledAt = session.CurrentTimestamp.AddMinutes(-5),
        };

        List<BacktestSession> sessions = [session];
        List<BacktestOrder> orders = [activeOrder];
        List<BacktestTradeResult> tradeResults = [];

        SetupTransactionalContext(sessions, orders, tradeResults);

        var result = await _handler.Handle(new FinishSession.Request(session.Id, 100m) { UserId = 42 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100.5m, activeOrder.ExitPrice);
        Assert.Equal(9.5m, activeOrder.Pnl);
        Assert.Single(tradeResults);
        Assert.Equal(100.5m, tradeResults[0].ExitPrice);
    }
}