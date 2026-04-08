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
        result.IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_NullAsset_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Asset = null! };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Asset"));
    }

    [Test]
    public void Validate_EmptyChecklists_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradeHistoryChecklists = [] };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("checklist"));
    }

    [Test]
    public void Validate_TradingZoneIdZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradingZoneId = 0 };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Trading Zone"));
    }

    [Test]
    public void Validate_InvalidPosition_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Position = (SharedEnums.PositionType)99 };
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
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
            [1], 1, 42);

        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Include(It.IsAny<string>())).Returns(tradeSet.Object);
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradeHistory?)null);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task Handle_TradeFound_UpdatesAndReturnsSuccess()
    {
        var request = new UpdateTrade.Request(
            1, "EURUSD", SharedEnums.PositionType.Long, 1.0850, 1.0900,
            null, null, 1.0800, "Updated", DateTime.UtcNow,
            SharedEnums.TradeStatus.Open, null, null, null, [],
            null, null, ConfidenceLevel.Neutral, null,
            [1], 1, 42);

        var trade = new TradeHistory { Id = 1, CreatedBy = 42, Asset = "GBPUSD",
            TradeEmotionTags = [], TradeChecklists = [], TradeTechnicalAnalysisTags = [], TradeScreenShots = [] };
        var tradeSet = new Mock<DbSet<TradeHistory>>();
        tradeSet.Setup(x => x.Include(It.IsAny<string>())).Returns(tradeSet.Object);
        _ctx.Setup(x => x.TradeHistories).Returns(tradeSet.Object);
        tradeSet.Setup(x => x.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<TradeHistory, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var emotionSet = new Mock<DbSet<TradeEmotionTag>>();
        _ctx.Setup(x => x.TradeEmotionTags).Returns(emotionSet.Object);
        var checklistSet = new Mock<DbSet<TradeHistoryChecklist>>();
        _ctx.Setup(x => x.TradeHistoryChecklist).Returns(checklistSet.Object);
        var tagSet = new Mock<DbSet<TradeTechnicalAnalysisTag>>();
        _ctx.Setup(x => x.TradeTechnicalAnalysisTags).Returns(tagSet.Object);
        var screenshotSet = new Mock<DbSet<TradeScreenShot>>();
        _ctx.Setup(x => x.TradeScreenShots).Returns(screenshotSet.Object);
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        trade.Notes.Should().Be("Updated");
    }
}
