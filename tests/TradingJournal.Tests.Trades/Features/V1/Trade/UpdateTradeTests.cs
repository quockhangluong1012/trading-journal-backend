using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using Microsoft.AspNetCore.Hosting;
using SharedEnums = TradingJournal.Shared.Common.Enum;
using TradingJournal.Modules.Trades.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

public sealed class UpdateTradeValidatorTests
{
    private UpdateTrade.Validator _validator = null!;

    public UpdateTradeValidatorTests() => _validator = new UpdateTrade.Validator();

    private UpdateTrade.Request CreateValidRequest() =>
        new(1, "EURUSD", SharedEnums.PositionType.Long, 1.0850, 1.0900,
            null, null, 1.0800, "Notes", DateTime.UtcNow,
            SharedEnums.TradeStatus.Open, null, null, null, [],
            null, null, ConfidenceLevel.Neutral, null,
            [1], 1, null);

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        var result = _validator.Validate(CreateValidRequest());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NullAsset_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Asset = null! };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Asset"));
    }

    [Fact]
    public void Validate_EmptyChecklists_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradeHistoryChecklists = [] };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("checklist"));
    }

    [Fact]
    public void Validate_TradingZoneIdZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradingZoneId = 0 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Trading Zone"));
    }

    [Fact]
    public void Validate_InvalidPosition_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Position = (SharedEnums.PositionType)99 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
    }
}

public sealed class UpdateTradeHandlerTests
{
    private Mock<ITradeDbContext> _ctx = null!;
    private Mock<IWebHostEnvironment> _env = null!;
    private UpdateTrade.Handler _handler = null!;

    public UpdateTradeHandlerTests()
    {
        _ctx = new Mock<ITradeDbContext>();
        _env = new Mock<IWebHostEnvironment>();
        _handler = new UpdateTrade.Handler(_ctx.Object, _env.Object);
    }

    [Fact]
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

        Assert.False(result.IsSuccess);
    }

    [Fact]
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
        _ctx.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist>
        {
            new() { Id = 1, Name = "Checklist 1", ChecklistModelId = 1, ChecklistModel = new ChecklistModel { Id = 1, Name = "Model", CreatedBy = 42 } }
        }.AsQueryable()).Object);

        _ctx.Setup(x => x.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _ctx.Setup(x => x.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _ctx.Setup(x => x.TradeTechnicalAnalysisTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeTechnicalAnalysisTag>().AsQueryable()).Object);
        _ctx.Setup(x => x.TradeScreenShots).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeScreenShot>().AsQueryable()).Object);
        _ctx.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess, $"Error: {result.Errors?.FirstOrDefault()?.Description}");
        Assert.True(result.Value);
        Assert.Equal("Updated", trade.Notes);
    }

    [Fact]
    public async Task Handle_Checklist_From_Another_User_ReturnsFailure()
    {
        var request = new UpdateTrade.Request(
            1, "EURUSD", SharedEnums.PositionType.Long, 1.0850, 1.0900,
            null, null, 1.0800, "Updated", DateTime.UtcNow,
            SharedEnums.TradeStatus.Open, null, null, null, [],
            null, null, ConfidenceLevel.Neutral, null,
            [1], 1, null, UserId: 42);

        var trade = new TradeHistory
        {
            Id = 1,
            CreatedBy = 42,
            Asset = "GBPUSD",
            TradeEmotionTags = [],
            TradeChecklists = [],
            TradeTechnicalAnalysisTags = [],
            TradeScreenShots = []
        };

        _ctx.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory> { trade }.AsQueryable()).Object);
        _ctx.Setup(x => x.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(new List<PretradeChecklist>
        {
            new() { Id = 1, Name = "Checklist 1", ChecklistModelId = 1, ChecklistModel = new ChecklistModel { Id = 1, Name = "Model", CreatedBy = 7 } }
        }.AsQueryable()).Object);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        _ctx.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
