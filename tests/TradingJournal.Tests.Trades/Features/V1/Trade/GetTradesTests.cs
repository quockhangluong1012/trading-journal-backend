using NUnit.Framework;
using FluentAssertions;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Shared.Interfaces;
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
        result.IsValid.Should().BeTrue();
    }
    [Test] public void Validate_PageZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetTrades.Request { Page = 0 });
        result.IsValid.Should().BeFalse();
    }
    [Test] public void Validate_PageSizeZero_ReturnsInvalid()
    {
        var result = _validator.Validate(new GetTrades.Request { Page = 1, PageSize = 0 });
        result.IsValid.Should().BeFalse();
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
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.AsNoTracking()).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        tradeSet.Setup(x => x.OrderByDescending(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, DateTime>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.Skip(It.IsAny<int>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.Take(It.IsAny<int>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeHistory>());
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var emoSet = new Mock<DbSet<TradeEmotionTag>>();
        emoSet.Setup(x => x.AsNoTracking()).Returns(emoSet.Object);
        emoSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeEmotionTag, bool>>>())).Returns(emoSet.Object);
        emoSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeEmotionTag>());
        _ctx.Setup(x => x.TradeEmotionTags).Returns(emoSet.Object);

        var result = await _handler.Handle(new GetTrades.Request { UserId = 1 }, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.Values.Should().BeEmpty();
    }
    [Test]
    public async Task Handle_HasResults_ReturnsPaginatedData()
    {
        var trades = new List<TradeHistory> { new() { Id = 1, Asset = "EURUSD", Position = SharedEnums.PositionType.Long, EntryPrice = 1.08, Date = DateTime.UtcNow, Status = SharedEnums.TradeStatus.Open, TargetTier1 = 1.09, StopLoss = 1.07 } };
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.AsNoTracking()).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        tradeSet.Setup(x => x.OrderByDescending(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, DateTime>>>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.Skip(It.IsAny<int>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.Take(It.IsAny<int>())).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);
        var emoSet = new Mock<DbSet<TradeEmotionTag>>();
        emoSet.Setup(x => x.AsNoTracking()).Returns(emoSet.Object);
        emoSet.Setup(x => x.Where(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeEmotionTag, bool>>>())).Returns(emoSet.Object);
        emoSet.Setup(x => x.ToListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeEmotionTag>());
        _ctx.Setup(x => x.TradeEmotionTags).Returns(emoSet.Object);
        _emoProvider.Setup(x => x.GetEmotionTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<EmotionTagCacheDto>());

        var result = await _handler.Handle(new GetTrades.Request { UserId = 1, Page = 1, PageSize = 10 }, CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
        result.Value!.Values.Should().HaveCount(1);
        result.Value.TotalItems.Should().Be(1);
    }
}
