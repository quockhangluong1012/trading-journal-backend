using TradingJournal.Tests.Trades.Helpers;
using NUnit.Framework;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

[TestFixture]
public sealed class CreateTradeValidatorTests
{
    private CreateTrade.Validator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new CreateTrade.Validator();
    }

    private CreateTrade.Request CreateValidRequest() =>
        new(
            Asset: "EURUSD",
            Position: SharedEnums.PositionType.Long,
            EntryPrice: 1.0850,
            TargetTier1: 1.0900,
            TargetTier2: null,
            TargetTier3: null,
            StopLoss: 1.0800,
            Notes: "Good setup",
            Date: DateTime.UtcNow,
            Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null,
            Pnl: null,
            ClosedDate: null,
            Screenshots: [],
            TradeTechnicalAnalysisTags: [],
            EmotionTags: [],
            ConfidenceLevel: TradingJournal.Modules.Trades.Common.Enum.ConfidenceLevel.Neutral,
            PsychologyNotes: null,
            TradeHistoryChecklists: [1],
            TradingZoneId: 1,
            TradingSessionId: null);

    [Test]
    public void Validate_ValidRequest_ReturnsValid()
    {
        var request = CreateValidRequest();
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_NullAsset_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Asset = null! };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorMessage.Contains("Asset")), Is.True);
    }

    [Test]
    public void Validate_InvalidPosition_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Position = (SharedEnums.PositionType)99 };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorMessage.Contains("Position")), Is.True);
    }

    [Test]
    public void Validate_EntryPriceZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { EntryPrice = 0 };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorMessage.Contains("EntryPrice")), Is.True);
    }

    [Test]
    public void Validate_EntryPriceNegative_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { EntryPrice = -1 };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_TargetTier1Zero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TargetTier1 = 0 };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorMessage.Contains("Target Tier")), Is.True);
    }

    [Test]
    public void Validate_StopLossZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { StopLoss = 0 };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorMessage.Contains("Stop Loss")), Is.True);
    }

    [Test]
    public void Validate_NullNotes_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Notes = null! };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorMessage.Contains("notes")), Is.True);
    }

    [Test]
    public void Validate_EmptyCheckoutLists_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradeHistoryChecklists = [] };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorMessage.Contains("checklist")), Is.True);
    }

    [Test]
    public void Validate_TradingZoneIdZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradingZoneId = 0 };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorMessage.Contains("Trading Zone")), Is.True);
    }

    [Test]
    public void Validate_InvalidStatus_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Status = (SharedEnums.TradeStatus)99 };
        var result = _validator.Validate(request);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorMessage.Contains("Status")), Is.True);
    }
}

[TestFixture]
public sealed class CreateTradeHandlerTests
{
    private Mock<ITradeDbContext> _contextMock = null!;
    private Mock<IWebHostEnvironment> _envMock = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private CreateTrade.Handler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _contextMock = new Mock<ITradeDbContext>();
        _envMock = new Mock<IWebHostEnvironment>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _handler = new CreateTrade.Handler(_contextMock.Object, _envMock.Object, _httpContextAccessorMock.Object);
    }

    [Test]
    public async Task Handle_ValidRequest_CreatesTradeAndReturnsSuccess()
    {
        var request = new CreateTrade.Request(
            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850,
            TargetTier1: 1.0900, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800,
            Notes: "Good setup", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
            TradeTechnicalAnalysisTags: [], EmotionTags: null,
            ConfidenceLevel: TradingJournal.Modules.Trades.Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null);

        _contextMock.Setup(c => c.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeScreenShots).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeScreenShot>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeTechnicalAnalysisTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeTechnicalAnalysisTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _contextMock.Setup(c => c.CommitTransaction()).Returns(Task.CompletedTask);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.GreaterThanOrEqualTo(0));
        _contextMock.Verify(c => c.BeginTransaction(), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(c => c.CommitTransaction(), Times.Once);
    }

    [Test]
    public async Task Handle_SaveChangesReturnsZero_ReturnsFailure()
    {
        var request = new CreateTrade.Request(
            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850,
            TargetTier1: 1.0900, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800,
            Notes: "Test", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
            TradeTechnicalAnalysisTags: [], EmotionTags: null,
            ConfidenceLevel: TradingJournal.Modules.Trades.Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null);

        _contextMock.Setup(c => c.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeScreenShots).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeScreenShot>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeTechnicalAnalysisTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeTechnicalAnalysisTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _contextMock.Setup(c => c.RollbackTransaction()).Returns(Task.CompletedTask);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
    }

    [Test]
    public async Task Handle_ExceptionOccurs_RollbacksAndReturnsFailure()
    {
        var request = new CreateTrade.Request(
            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850,
            TargetTier1: 1.0900, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800,
            Notes: "Test", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
            TradeTechnicalAnalysisTags: [], EmotionTags: null,
            ConfidenceLevel: TradingJournal.Modules.Trades.Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null);

        _contextMock.Setup(c => c.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeScreenShots).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeScreenShot>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeTechnicalAnalysisTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeTechnicalAnalysisTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));
        _contextMock.Setup(c => c.RollbackTransaction()).Returns(Task.CompletedTask);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors.Any(e => e.Description.Contains("DB error")), Is.True);
        _contextMock.Verify(c => c.RollbackTransaction(), Times.Once);
    }
}
