//using NUnit.Framework;
//using FluentAssertions;
//using Moq;
//using TradingJournal.Messaging.Shared.Abstractions;
//using TradingJournal.Modules.Trades.Features.V1.Trade;
//using TradingJournal.Modules.Trades.Infrastructure;
//using TradingJournal.Modules.Trades.Domain;
//using TradingJournal.Modules.Trades.Events;
//using SharedEnums = TradingJournal.Shared.Common.Enum;

//namespace TradingJournal.Tests.Trades.Features.V1.Trade;

//[TestFixture]
//public sealed class CloseTradeValidatorTests
//{
//    private CloseTrade.Validator _validator = null!;
//    [SetUp] public void SetUp() => _validator = new CloseTrade.Validator();

//    [Test] public void Validate_ValidRequest_ReturnsValid()
//    {
//        var result = _validator.Validate(new CloseTrade.Request(1, 1.1, 50.0, "ok", false, 42));
//        result.IsValid.Should().BeTrue();
//    }
//    [Test] public void Validate_TradeIdZero_ReturnsInvalid()
//    {
//        var r = _validator.Validate(new CloseTrade.Request(0, 1.1, 50.0, null, false, 42));
//        r.IsValid.Should().BeFalse();
//        r.Errors.Should().Contain(e => e.ErrorMessage.Contains("Trade ID"));
//    }
//    [Test] public void Validate_ExitPriceZero_ReturnsInvalid()
//    {
//        var r = _validator.Validate(new CloseTrade.Request(1, 0, 50.0, null, false, 42));
//        r.IsValid.Should().BeFalse();
//        r.Errors.Should().Contain(e => e.ErrorMessage.Contains("Exit price"));
//    }
//}

//[TestFixture]
//public sealed class CloseTradeHandlerTests
//{
//    private Mock<ITradeDbContext> _ctx = null!, _ = null!, eventBus = null!;
//    private CloseTrade.Handler _handler = null!;
//    [SetUp]
//    public void SetUp()
//    {
//        _ctx = new Mock<ITradeDbContext>();
//        eventBus = new Mock<IEventBus>();
//        _handler = new CloseTrade.Handler(_ctx.Object, eventBus.Object);
//    }

//    [Test]
//    public async Task Handle_TradeNotFound_ReturnsFailure()
//    {
//        var tradeSet = new Mock<DbSet<TradeHistory>>();
//        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);
//        tradeSet.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((TradeHistory?)null);
//        var result = await _handler.Handle(new CloseTrade.Request(1, 1.1, 50.0, null, false, 42), CancellationToken.None);
//        result.IsSuccess.Should().BeFalse();
//    }

//    [Test]
//    public async Task Handle_TradeFound_ClosesTradeAndPublishesEvent()
//    {
//        var trade = new TradeHistory { Id = 1, CreatedBy = 42, Status = SharedEnums.TradeStatus.Open };
//        var tradeSet = new Mock<DbSet<TradeHistory>>();
//        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);
//        tradeSet.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(trade);
//        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
//        eventBus.Setup(x => x.PublishAsync(It.IsAny<SummarizeTradingOrderEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
//        var result = await _handler.Handle(new CloseTrade.Request(1, 1.1, 50.0, "ok", false, 42), CancellationToken.None);
//        result.IsSuccess.Should().BeTrue();
//        trade.ExitPrice.Should().Be(1.1);
//        trade.Status.Should().Be(SharedEnums.TradeStatus.Closed);
//        trade.ClosedDate.Should().NotBeNull();
//        eventBus.Verify(x => x.PublishAsync(It.IsAny<SummarizeTradingOrderEvent>(), It.IsAny<CancellationToken>()), Times.Once);
//    }
//}
