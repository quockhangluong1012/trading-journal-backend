using TradingJournal.Tests.Trades.Helpers;
using NUnit.Framework;
using Moq;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Messaging.Shared.Abstractions;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Events;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

[TestFixture]
public sealed class CloseTradeValidatorTests
{
    private CloseTrade.Validator _validator = null!;
    [SetUp] public void SetUp() => _validator = new CloseTrade.Validator();

    [Test] public void Validate_ValidRequest_ReturnsValid()
    {
        var result = _validator.Validate(new CloseTrade.Request(1, 1.1, 50.0, "ok", false, 42));
        Assert.That(result.IsValid, Is.True);
    }
    [Test] public void Validate_TradeIdZero_ReturnsInvalid()
    {
        var r = _validator.Validate(new CloseTrade.Request(0, 1.1, 50.0, null, false, 42));
        Assert.That(r.IsValid, Is.False);
        Assert.That(r.Errors.Any(e => e.ErrorMessage.Contains("Trade ID")), Is.True);
    }
    [Test] public void Validate_ExitPriceZero_ReturnsInvalid()
    {
        var r = _validator.Validate(new CloseTrade.Request(1, 0, 50.0, null, false, 42));
        Assert.That(r.IsValid, Is.False);
        Assert.That(r.Errors.Any(e => e.ErrorMessage.Contains("Exit price")), Is.True);
    }
}

[TestFixture]
public sealed class CloseTradeHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<IEventBus> eventBus = null!;
    private CloseTrade.Handler _handler = null!;
    [SetUp]
    public void SetUp()
    {
        _ctx = new Mock<ITradeDbContext>();
        eventBus = new Mock<IEventBus>();
        _handler = new CloseTrade.Handler(_ctx.Object, eventBus.Object);
    }

    [Test]
    public async Task Handle_TradeNotFound_ReturnsFailure()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        var result = await _handler.Handle(new CloseTrade.Request(1, 1.1, 50.0, null, false, 42), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public async Task Handle_TradeFound_ClosesTradeAndPublishesEvent()
    {
        var trade = new TradeHistory { Id = 1, CreatedBy = 42, Status = SharedEnums.TradeStatus.Open };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory> { trade }.AsQueryable()).Object);
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        eventBus.Setup(x => x.PublishAsync(It.IsAny<SummarizeTradingOrderEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var result = await _handler.Handle(new CloseTrade.Request(1, 1.1, 50.0, "ok", false, 42), CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(trade.ExitPrice, Is.EqualTo(1.1));
        Assert.That(trade.Status, Is.EqualTo(SharedEnums.TradeStatus.Closed));
        Assert.That(trade.ClosedDate, Is.Not.Null);
        eventBus.Verify(x => x.PublishAsync(It.IsAny<SummarizeTradingOrderEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
