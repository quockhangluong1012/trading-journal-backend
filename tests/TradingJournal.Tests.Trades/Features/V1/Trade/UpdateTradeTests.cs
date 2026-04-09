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
using TradingJournal.Modules.Trades.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

[TestFixture]
public sealed class UpdateTradeValidatorTests
{
    private UpdateTrade.Validator _validator = null!;

    [SetUp]
    public void SetUp() => _validator = new UpdateTrade.Validator();

    private UpdateTrade.Request CreateValidRequest() =>
        new(1, "EURUSD", SharedEnums.PositionType.Long, 1.0850, 1.0900,
            null, null, 1.0800, "Notes", DateTime.UtcNow,
            SharedEnums.TradeStatus.Open, null, null, null, [],
            null, null, ConfidenceLevel.Neutral, null,
            [1], 1, null);

    [Test]
    public void Validate_ValidRequest_ReturnsValid()
    {
        var result = _validator.Validate(CreateValidRequest());
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_NullAsset_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Asset = null! };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Asset"));
    }

    [Test]
    public void Validate_EmptyChecklists_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradeHistoryChecklists = [] };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("checklist"));
    }

    [Test]
    public void Validate_TradingZoneIdZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradingZoneId = 0 };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Trading Zone"));
    }

    [Test]
    public void Validate_InvalidPosition_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Position = (SharedEnums.PositionType)99 };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
    }
}

[TestFixture]
public sealed class UpdateTradeHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<IWebHostEnvironment> _env = null!;
    private UpdateTrade.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _ctx = new Mock<ITradeDbContext>();
        _env = new Mock<IWebHostEnvironment>();
        _handler = new UpdateTrade.Handler(_ctx.Object, _env.Object);
    }

    [Test]
    public async Task Handle_TradeNotFound_ReturnsFailure()
    {
        var request = new UpdateTrade.Request(
            1, "EURUSD", SharedEnums.PositionType.Long, 1.0850, 1.0900,
            null, null, 1.0800, "Notes", DateTime.UtcNow,
            SharedEnums.TradeStatus.Open, null, null, null, [],
            null, null, ConfidenceLevel.Neutral, null,
            [1], 1, null, UserId: 42);

        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public async Task Handle_TradeFound_UpdatesAndReturnsSuccess()
    {
        var request = new UpdateTrade.Request(
            1, "EURUSD", SharedEnums.PositionType.Long, 1.0850, 1.0900,
            null, null, 1.0800, "Updated", DateTime.UtcNow,
            SharedEnums.TradeStatus.Open, null, null, null, [],
            null, null, ConfidenceLevel.Neutral, null,
            [1], 1, null, UserId: 42);

        var trade = new TradeHistory { Id = 1, CreatedBy = 42, Asset = "GBPUSD",
            TradeEmotionTags = [], TradeChecklists = [], TradeTechnicalAnalysisTags = [], TradeScreenShots = [] };
        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory> { trade }.AsQueryable()).Object);

        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _ctx.Setup(x => x.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _ctx.Setup(x => x.TradeTechnicalAnalysisTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeTechnicalAnalysisTag>().AsQueryable()).Object);
        _ctx.Setup(x => x.TradeScreenShots).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeScreenShot>().AsQueryable()).Object);
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True, $"Error: {result.Errors?.FirstOrDefault()?.Description}");
        Assert.That(result.Value, Is.True);
        Assert.That(trade.Notes, Is.EqualTo("Updated"));
    }
}
