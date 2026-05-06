using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Shared.Interfaces;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

public sealed class CloseTradeValidatorTests
{
    private CloseTrade.Validator _validator = null!;
    public CloseTradeValidatorTests() => _validator = new CloseTrade.Validator();

    [Fact] public void Validate_ValidRequest_ReturnsValid()
    {
        var result = _validator.Validate(new CloseTrade.Request(1, 1.1m, 50.0m, "ok", false, null, 42));
        Assert.True(result.IsValid);
    }
    [Fact] public void Validate_TradeIdZero_ReturnsInvalid()
    {
        var r = _validator.Validate(new CloseTrade.Request(0, 1.1m, 50.0m, null, false, null, 42));
        Assert.False(r.IsValid);
        Assert.True(r.Errors.Any(e => e.ErrorMessage.Contains("Trade ID")));
    }
    [Fact] public void Validate_ExitPriceZero_ReturnsInvalid()
    {
        var r = _validator.Validate(new CloseTrade.Request(1, 0m, 50.0m, null, false, null, 42));
        Assert.False(r.IsValid);
        Assert.True(r.Errors.Any(e => e.ErrorMessage.Contains("Exit price")));
    }
}

public sealed class CloseTradeHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private CloseTrade.Handler _handler = null!;
    public CloseTradeHandlerTests()
    {
        _ctx = new Mock<ITradeDbContext>();
        _handler = new CloseTrade.Handler(_ctx.Object, new Mock<ICacheRepository>().Object);
    }

    [Fact]
    public async Task Handle_TradeNotFound_ReturnsFailure()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        var result = await _handler.Handle(new CloseTrade.Request(1, 1.1m, 50.0m, null, false, null, 42), CancellationToken.None);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_TradeFound_ClosesTradeSuccessfully()
    {
        var trade = new TradeHistory { Id = 1, CreatedBy = 42, Status = SharedEnums.TradeStatus.Open };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory> { trade }.AsQueryable()).Object);
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var result = await _handler.Handle(new CloseTrade.Request(1, 1.1m, 50.0m, "ok", false, null, 42), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(1.1m, trade.ExitPrice);
        Assert.Equal(SharedEnums.TradeStatus.Closed, trade.Status);
        Assert.NotNull(trade.ClosedDate);
    }
}
