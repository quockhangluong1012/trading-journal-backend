using NUnit.Framework;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Tests.Trades.Helpers;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

[TestFixture]
public sealed class GetTradesValidatorTests
{
    private GetTrades.Validator _validator = null!;
    [SetUp] public void SetUp() => _validator = new GetTrades.Validator();
    [Test] public void Validate_DefaultValues_ReturnsValid()
    {
        var result = _validator.Validate(new GetTrades.Request { UserId = 1 });
        Assert.That(result.IsValid, Is.True);
    }
    [Test] public void Validate_PageZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetTrades.Request { Page = 0 });
        Assert.That(result.IsValid, Is.False);
    }
    [Test] public void Validate_PageSizeZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetTrades.Request { Page = 1, PageSize = 0 });
        Assert.That(result.IsValid, Is.False);
    }
}

[TestFixture]
public sealed class GetTradesHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<ICacheRepository> _cache = null!;
    private Mock<IEmotionTagProvider> _emoProvider = null!;
    private GetTrades.Handler _handler = null!;
    [SetUp]
    public void SetUp()
    {
        _ctx = new Mock<ITradeDbContext>();
        _cache = new Mock<ICacheRepository>();
        _emoProvider = new Mock<IEmotionTagProvider>();
        _handler = new GetTrades.Handler(_ctx.Object, _cache.Object, _emoProvider.Object);
    }
    [Test]
    public async Task Handle_EmptyResults_ReturnsSuccess()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetTrades.Request { UserId = 1 }, CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Values, Is.Empty);
    }
    [Test]
    public async Task Handle_HasResults_ReturnsPaginatedData()
    {
        var trades = new List<TradeHistory> { new() { Id = 1, Asset = "EURUSD", Position = SharedEnums.PositionType.Long, EntryPrice = 1.08, Date = DateTime.UtcNow, Status = SharedEnums.TradeStatus.Open, TargetTier1 = 1.09, StopLoss = 1.07, CreatedBy = 1 } };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);
        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EmotionTagCacheDto>());

        var result = await _handler.Handle(new GetTrades.Request { UserId = 1, Page = 1, PageSize = 10 }, CancellationToken.None);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Values, Has.Count.EqualTo(1));
        Assert.That(result.Value.TotalItems, Is.EqualTo(1));
    }
}
