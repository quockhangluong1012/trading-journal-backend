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

public sealed class GetTradesValidatorTests
{
    private GetTrades.Validator _validator = null!;
    public GetTradesValidatorTests() => _validator = new GetTrades.Validator();
    [Fact] public void Validate_DefaultValues_ReturnsValid()
    {
        var result = _validator.Validate(new GetTrades.Request { UserId = 1 });
        Assert.True(result.IsValid);
    }
    [Fact] public void Validate_PageZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetTrades.Request { Page = 0 });
        Assert.False(result.IsValid);
    }
    [Fact] public void Validate_PageSizeZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetTrades.Request { Page = 1, PageSize = 0 });
        Assert.False(result.IsValid);
    }
}

public sealed class GetTradesHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<ICacheRepository> _cache = null!;
    private Mock<IEmotionTagProvider> _emoProvider = null!;
    private GetTrades.Handler _handler = null!;
    public GetTradesHandlerTests()
    {
        _ctx = new Mock<ITradeDbContext>();
        _cache = new Mock<ICacheRepository>();
        _emoProvider = new Mock<IEmotionTagProvider>();
        _handler = new GetTrades.Handler(_ctx.Object, _cache.Object, _emoProvider.Object);
    }
    [Fact]
    public async Task Handle_EmptyResults_ReturnsSuccess()
    {
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);

        var result = await _handler.Handle(new GetTrades.Request { UserId = 1 }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Values);
    }
    [Fact]
    public async Task Handle_HasResults_ReturnsPaginatedData()
    {
        var trades = new List<TradeHistory> { new() { Id = 1, Asset = "EURUSD", Position = SharedEnums.PositionType.Long, EntryPrice = 1.08, Date = DateTime.UtcNow, Status = SharedEnums.TradeStatus.Open, TargetTier1 = 1.09, StopLoss = 1.07, CreatedBy = 1 } };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(trades.AsQueryable()).Object);
        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EmotionTagCacheDto>());

        var result = await _handler.Handle(new GetTrades.Request { UserId = 1, Page = 1, PageSize = 10 }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Values.Count);
        Assert.Equal(1, result.Value.TotalItems);
    }
}
