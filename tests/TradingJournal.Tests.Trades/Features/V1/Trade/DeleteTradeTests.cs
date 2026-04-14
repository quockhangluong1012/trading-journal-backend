using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using Microsoft.AspNetCore.Hosting;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

public sealed class DeleteTradeValidatorTests
{
    private DeleteTrade.Validator _validator = null!;
    public DeleteTradeValidatorTests() => _validator = new DeleteTrade.Validator();

    [Fact] public void Validate_ValidId_ReturnsValid()
    {
        var result = _validator.Validate(new DeleteTrade.Request { Id = 1 });
        Assert.True(result.IsValid);
    }
    [Fact] public void Validate_IdZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new DeleteTrade.Request { Id = 0 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Trade ID"));
    }
}

public sealed class DeleteTradeHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<IWebHostEnvironment> _env = null!;
    private DeleteTrade.Handler _handler = null!;

    public DeleteTradeHandlerTests()
    {
        _ctx = new Mock<ITradeDbContext>();
        _env = new Mock<IWebHostEnvironment>();
        _env.Setup(x => x.ContentRootPath).Returns("/tmp");
        _handler = new DeleteTrade.Handler(_ctx.Object, _env.Object);
    }
    [Fact]
    public async Task Handle_TradeNotFound_ReturnsFailure()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        var result = await _handler.Handle(new DeleteTrade.Request { Id = 1, UserId = 42 }, CancellationToken.None);
        Assert.False(result.IsSuccess);
    }
    [Fact]
    public async Task Handle_TradeFound_DeletesAndReturnsSuccess()
    {
        var trade = new TradeHistory { Id = 1, CreatedBy = 42, TradeScreenShots = new List<TradeScreenShot>(), TradeChecklists = new List<TradeHistoryChecklist>(), TradeTechnicalAnalysisTags = new List<TradeTechnicalAnalysisTag>() };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory> { trade }.AsQueryable()).Object);
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var result = await _handler.Handle(new DeleteTrade.Request { Id = 1, UserId = 42 }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        _ctx.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
