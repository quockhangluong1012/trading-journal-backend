using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using SharedEnums = TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Trades.Features.V1.Trade;

public sealed class CreateTradeValidatorTests
{
    private CreateTrade.Validator _validator = null!;

    public CreateTradeValidatorTests()
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

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        var request = CreateValidRequest();
        var result = _validator.Validate(request);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NullAsset_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Asset = null! };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Any(e => e.ErrorMessage.Contains("Asset")));
    }

    [Fact]
    public void Validate_InvalidPosition_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Position = (SharedEnums.PositionType)99 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Any(e => e.ErrorMessage.Contains("Position")));
    }

    [Fact]
    public void Validate_EntryPriceZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { EntryPrice = 0 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Any(e => e.ErrorMessage.Contains("EntryPrice")));
    }

    [Fact]
    public void Validate_EntryPriceNegative_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { EntryPrice = -1 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_TargetTier1Zero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TargetTier1 = 0 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Any(e => e.ErrorMessage.Contains("Target Tier")));
    }

    [Fact]
    public void Validate_StopLossZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { StopLoss = 0 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Any(e => e.ErrorMessage.Contains("Stop Loss")));
    }

    [Fact]
    public void Validate_NullNotes_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Notes = null! };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Any(e => e.ErrorMessage.Contains("notes")));
    }

    [Fact]
    public void Validate_EmptyCheckoutLists_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradeHistoryChecklists = [] };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Any(e => e.ErrorMessage.Contains("checklist")));
    }

    [Fact]
    public void Validate_TradingZoneIdZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { TradingZoneId = 0 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Any(e => e.ErrorMessage.Contains("Trading Zone")));
    }

    [Fact]
    public void Validate_InvalidStatus_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Status = (SharedEnums.TradeStatus)99 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Any(e => e.ErrorMessage.Contains("Status")));
    }
}

public sealed class CreateTradeHandlerTests
{
    private Mock<ITradeDbContext> _contextMock = null!;
    private Mock<IWebHostEnvironment> _envMock = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private CreateTrade.Handler _handler = null!;

    public CreateTradeHandlerTests()
    {
        _contextMock = new Mock<ITradeDbContext>();
        _envMock = new Mock<IWebHostEnvironment>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _handler = new CreateTrade.Handler(_contextMock.Object, _envMock.Object, _httpContextAccessorMock.Object);
    }

    private void SetCurrentUser(int userId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            ], "TestAuth"))
        };

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private void SetupAccessibleChecklists(int userId, params int[] checklistIds)
    {
        var model = new ChecklistModel { Id = 1, Name = "Model", CreatedBy = userId };
        var checklists = checklistIds.Select(checklistId => new PretradeChecklist
        {
            Id = checklistId,
            Name = $"Checklist {checklistId}",
            ChecklistModelId = model.Id,
            ChecklistModel = model,
        }).ToList();

        _contextMock.Setup(c => c.PretradeChecklists).Returns(DbSetMockHelper.CreateMockDbSet(checklists.AsQueryable()).Object);
    }

    private void SetupTradingProfiles()
    {
        _contextMock.Setup(c => c.TradingProfiles).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingProfile>().AsQueryable()).Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesTradeAndReturnsSuccess()
    {
        SetCurrentUser(42);
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
        SetupAccessibleChecklists(42, 1);
        SetupTradingProfiles();
        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _contextMock.Setup(c => c.CommitTransaction()).Returns(Task.CompletedTask);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value >= 0);
        _contextMock.Verify(c => c.BeginTransaction(), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(c => c.CommitTransaction(), Times.Once);
    }

    [Fact]
    public async Task Handle_SaveChangesReturnsZero_ReturnsFailure()
    {
        SetCurrentUser(42);
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
        SetupAccessibleChecklists(42, 1);
        SetupTradingProfiles();
        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _contextMock.Setup(c => c.RollbackTransaction()).Returns(Task.CompletedTask);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ExceptionOccurs_RollbacksAndReturnsFailure()
    {
        SetCurrentUser(42);
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
        SetupAccessibleChecklists(42, 1);
        SetupTradingProfiles();
        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));
        _contextMock.Setup(c => c.RollbackTransaction()).Returns(Task.CompletedTask);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.Errors.Any(e => e.Description.Contains("DB error")));
        _contextMock.Verify(c => c.RollbackTransaction(), Times.Once);
    }

    [Fact]
    public async Task Handle_Checklist_From_Another_User_Returns_Failure()
    {
        SetCurrentUser(42);
        var request = new CreateTrade.Request(
            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850,
            TargetTier1: 1.0900, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800,
            Notes: "Test", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
            TradeTechnicalAnalysisTags: [], EmotionTags: null,
            ConfidenceLevel: TradingJournal.Modules.Trades.Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null);

        _contextMock.Setup(c => c.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
        _contextMock.Setup(c => c.RollbackTransaction()).Returns(Task.CompletedTask);
        SetupAccessibleChecklists(7, 1);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _contextMock.Verify(c => c.RollbackTransaction(), Times.Once);
    }
}
