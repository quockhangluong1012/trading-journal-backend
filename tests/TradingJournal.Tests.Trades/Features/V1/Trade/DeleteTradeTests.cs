using TradingJournal.Tests.Trades.Helpers;
using NUnit.Framework;
using FluentAssertions;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using Microsoft.AspNetCore.Hosting;
using SharedEnums = TradingJournal.Shared.Common.Enum;
using Microsoft.EntityFrameworkCore;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

[TestFixture]
public sealed class DeleteTradeValidatorTests
{
    private DeleteTrade.Validator _validator = null!;
    [SetUp] public void SetUp() => _validator = new DeleteTrade.Validator();

    [Test] public void Validate_ValidId_ReturnsValid()
    {
        var result = _validator.Validate(new DeleteTrade.Request { Id = 1 });
        Assert.That(result.IsValid, Is.True);
    }
    [Test] public void Validate_IdZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new DeleteTrade.Request { Id = 0 });
        Assert.That(result.IsValid, Is.False);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Trade ID"));
    }
}

[TestFixture]
public sealed class DeleteTradeHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<IWebHostEnvironment> _env = null!;
    private DeleteTrade.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _ctx = new Mock<ITradeDbContext>();
        _env = new Mock<IWebHostEnvironment>();
        _env.Setup(x => x.ContentRootPath).Returns("/tmp");
        _handler = new DeleteTrade.Handler(_ctx.Object, _env.Object);
    }
    [Test]
    public async Task Handle_TradeNotFound_ReturnsFailure()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        var result = await _handler.Handle(new DeleteTrade.Request { Id = 1, UserId = 42 }, CancellationToken.None);
        Assert.That(result.IsSuccess, Is.False);
    }
    [Test]
    public async Task Handle_TradeFound_DeletesAndReturnsSuccess()
    {
        var trade = new TradeHistory { Id = 1, CreatedBy = 42, TradeScreenShots = new List<TradeScreenShot>(), TradeChecklists = new List<TradeHistoryChecklist>(), TradeTechnicalAnalysisTags = new List<TradeTechnicalAnalysisTag>() };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory> { trade }.AsQueryable()).Object);
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var result = await _handler.Handle(new DeleteTrade.Request { Id = 1, UserId = 42 }, CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(1));
        _ctx.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
