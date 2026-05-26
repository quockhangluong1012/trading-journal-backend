using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Features.V1.Orders;
using TradingJournal.Modules.Backtest.Infrastructure;
using TradingJournal.Tests.Backtest.Helpers;

namespace TradingJournal.Tests.Backtest.Features.V1.Orders;

#region Validator

public sealed class PlaceOrderValidatorTests
{
    private static readonly PlaceOrder.Validator _validator = new();

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var request = new PlaceOrder.Request(1, BacktestOrderType.Market, BacktestOrderSide.Long, 1.0850m, 1m, 1.0800m, 1.0900m);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_SessionId_Is_Zero()
    {
        var request = new PlaceOrder.Request(0, BacktestOrderType.Market, BacktestOrderSide.Long, 1.0850m, 1m, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Should_Have_Error_When_PositionSize_Is_Zero()
    {
        var request = new PlaceOrder.Request(1, BacktestOrderType.Market, BacktestOrderSide.Long, 1.0850m, 0m, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PositionSize);
    }

    [Fact]
    public void Should_Have_Error_When_EntryPrice_Is_Zero()
    {
        var request = new PlaceOrder.Request(1, BacktestOrderType.Market, BacktestOrderSide.Long, 0m, 1m, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.EntryPrice);
    }

    [Fact]
    public void Should_Have_Error_When_OrderType_Is_Invalid()
    {
        var request = new PlaceOrder.Request(1, (BacktestOrderType)99, BacktestOrderSide.Long, 1.0850m, 1m, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.OrderType);
    }

    [Fact]
    public void Should_Have_Error_When_Side_Is_Invalid()
    {
        var request = new PlaceOrder.Request(1, BacktestOrderType.Market, (BacktestOrderSide)99, 1.0850m, 1m, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Side);
    }

    [Fact]
    public void Should_Not_Have_Error_When_StopLoss_And_TakeProfit_Are_Null()
    {
        var request = new PlaceOrder.Request(1, BacktestOrderType.Limit, BacktestOrderSide.Short, 1.0900m, 2m, null, null);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

#endregion

#region Handler

public sealed class PlaceOrderHandlerTests
{
    private readonly Mock<IBacktestDbContext> _context = new();
    private readonly PlaceOrder.Handler _handler;

    public PlaceOrderHandlerTests()
    {
        _handler = new PlaceOrder.Handler(_context.Object);
    }

    [Fact]
    public async Task Handle_MarketOrder_SetsStatusToActive()
    {
        var session = new BacktestSession
        {
            Id = 1,
            CreatedBy = 42,
            Status = BacktestSessionStatus.InProgress,
            CurrentTimestamp = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession> { session }.AsQueryable()).Object);
        _context.Setup(x => x.BacktestOrders)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestOrder>().AsQueryable()).Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new PlaceOrder.Request(1, BacktestOrderType.Market, BacktestOrderSide.Long, 1.0850m, 1m, 1.0800m, 1.0900m)
        {
            UserId = 42
        };

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value.Status);
        Assert.Equal(1.0850m, result.Value.FilledPrice);
    }

    [Fact]
    public async Task Handle_LimitOrder_SetsStatusToPending()
    {
        var session = new BacktestSession
        {
            Id = 1,
            CreatedBy = 42,
            Status = BacktestSessionStatus.InProgress,
            CurrentTimestamp = new DateTime(2024, 3, 1, 10, 0, 0, DateTimeKind.Utc)
        };

        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession> { session }.AsQueryable()).Object);
        _context.Setup(x => x.BacktestOrders)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestOrder>().AsQueryable()).Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new PlaceOrder.Request(1, BacktestOrderType.Limit, BacktestOrderSide.Long, 1.0800m, 1m, 1.0750m, 1.0850m)
        {
            UserId = 42
        };

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Null(result.Value.FilledPrice);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenSessionNotFound()
    {
        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession>().AsQueryable()).Object);

        var request = new PlaceOrder.Request(999, BacktestOrderType.Market, BacktestOrderSide.Long, 1.0850m, 1m, null, null)
        {
            UserId = 42
        };

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenSessionNotInProgress()
    {
        var session = new BacktestSession
        {
            Id = 1,
            CreatedBy = 42,
            Status = BacktestSessionStatus.Completed,
            CurrentTimestamp = DateTime.UtcNow
        };

        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession> { session }.AsQueryable()).Object);

        var request = new PlaceOrder.Request(1, BacktestOrderType.Market, BacktestOrderSide.Long, 1.0850m, 1m, null, null)
        {
            UserId = 42
        };

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenWrongUser()
    {
        var session = new BacktestSession
        {
            Id = 1,
            CreatedBy = 42,
            Status = BacktestSessionStatus.InProgress,
            CurrentTimestamp = DateTime.UtcNow
        };

        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession> { session }.AsQueryable()).Object);

        var request = new PlaceOrder.Request(1, BacktestOrderType.Market, BacktestOrderSide.Long, 1.0850m, 1m, null, null)
        {
            UserId = 99
        };

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}

#endregion
