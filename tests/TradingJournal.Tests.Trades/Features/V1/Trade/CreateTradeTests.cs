using TradingJournal.Tests.Trades.Helpers;
using Moq;
using TradingJournal.Modules.Trades.Features.V1.Trade;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Services;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using TradingJournal.Shared.Abstractions;
using TradingJournal.Shared.Interfaces;
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
            EntryPrice: 1.0850m,
            TargetTier1: 1.0900m,
            TargetTier2: null,
            TargetTier3: null,
            StopLoss: 1.0800m,
            Notes: "Good setup",
            Date: DateTime.UtcNow,
            Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null,
            Pnl: null,
            ClosedDate: null,
            Screenshots: [],
            TradeTechnicalAnalysisTags: [],
            EmotionTags: [],
            ConfidenceLevel: TradingJournal.Shared.Common.Enum.ConfidenceLevel.Neutral,
            PsychologyNotes: null,
            TradeHistoryChecklists: [1],
            TradingZoneId: 1,
            TradingSessionId: null,
            TradingSetupId: null);

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
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Asset"));
    }

    [Fact]
    public void Validate_InvalidPosition_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Position = (SharedEnums.PositionType)99 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Position"));
    }

    [Fact]
    public void Validate_EntryPriceZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { EntryPrice = 0 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("EntryPrice"));
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
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Target Tier"));
    }

    [Fact]
    public void Validate_StopLossZero_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { StopLoss = 0 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Stop Loss"));
    }

    [Fact]
    public void Validate_NullNotes_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Notes = null! };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("notes"));
    }

    [Fact]
    public void Validate_EmptyCheckoutLists_ReturnsInvalid()
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
    public void Validate_InvalidStatus_ReturnsInvalid()
    {
        var request = CreateValidRequest() with { Status = (SharedEnums.TradeStatus)99 };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Status"));
    }
}

public sealed class CreateTradeHandlerTests
{
    private Mock<ITradeDbContext> _contextMock = null!;
    private Mock<IScreenshotService> _screenshotMock = null!;
    private Mock<IDisciplineEvaluator> _disciplineMock = null!;
    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
    private Mock<ISetupProvider> _setupProviderMock = null!;
    private CreateTrade.Handler _handler = null!;

    public CreateTradeHandlerTests()
    {
        _contextMock = new Mock<ITradeDbContext>();
        _screenshotMock = new Mock<IScreenshotService>();
        _disciplineMock = new Mock<IDisciplineEvaluator>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _setupProviderMock = new Mock<ISetupProvider>();
        _handler = new CreateTrade.Handler(_contextMock.Object, _screenshotMock.Object, _disciplineMock.Object, _httpContextAccessorMock.Object, _setupProviderMock.Object, new Mock<ICacheRepository>().Object);
    }

    private void SetupTransactionalExecution()
    {
        _contextMock
            .Setup(c => c.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<Result<int>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task<Result<int>>> operation, CancellationToken ct) => operation(ct));
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
            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850m,
            TargetTier1: 1.0900m, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800m,
            Notes: "Good setup", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
            TradeTechnicalAnalysisTags: [], EmotionTags: null,
            ConfidenceLevel: TradingJournal.Shared.Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null, TradingSetupId: null);

        _contextMock.Setup(c => c.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeScreenShots).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeScreenShot>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeTechnicalAnalysisTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeTechnicalAnalysisTag>().AsQueryable()).Object);
        SetupAccessibleChecklists(42, 1);
        _setupProviderMock.Setup(x => x.HasSetupAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupTradingProfiles();
        SetupTransactionalExecution();
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value >= 0);
        _contextMock.Verify(c => c.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task<Result<int>>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SaveChangesReturnsZero_ReturnsFailure()
    {
        SetCurrentUser(42);
        var request = new CreateTrade.Request(
            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850m,
            TargetTier1: 1.0900m, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800m,
            Notes: "Test", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
            TradeTechnicalAnalysisTags: [], EmotionTags: null,
            ConfidenceLevel: TradingJournal.Shared.Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null, TradingSetupId: null);

        _contextMock.Setup(c => c.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeScreenShots).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeScreenShot>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeTechnicalAnalysisTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeTechnicalAnalysisTag>().AsQueryable()).Object);
        SetupAccessibleChecklists(42, 1);
        SetupTradingProfiles();
        SetupTransactionalExecution();
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ExceptionOccurs_RollbacksAndReturnsFailure()
    {
        SetCurrentUser(42);
        var request = new CreateTrade.Request(
            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850m,
            TargetTier1: 1.0900m, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800m,
            Notes: "Test", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
            TradeTechnicalAnalysisTags: [], EmotionTags: null,
            ConfidenceLevel: TradingJournal.Shared.Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null);

        _contextMock.Setup(c => c.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeScreenShots).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeScreenShot>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeTechnicalAnalysisTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeTechnicalAnalysisTag>().AsQueryable()).Object);
        SetupAccessibleChecklists(42, 1);
        _setupProviderMock.Setup(x => x.HasSetupAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupTradingProfiles();
        SetupTransactionalExecution();
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Description.Contains("DB error"));
    }

    [Fact]
    public async Task Handle_Checklist_From_Another_User_Returns_Failure()
    {
        SetCurrentUser(42);
        var request = new CreateTrade.Request(
            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850m,
            TargetTier1: 1.0900m, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800m,
            Notes: "Test", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
            TradeTechnicalAnalysisTags: [], EmotionTags: null,
            ConfidenceLevel: TradingJournal.Shared.Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null, TradingSetupId: null);

        _contextMock.Setup(c => c.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        SetupAccessibleChecklists(7, 1);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithTradingSetupId_AssignsSetupToTrade()
    {
        SetCurrentUser(42);
        var request = new CreateTrade.Request(
            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850m,
            TargetTier1: 1.0900m, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800m,
            Notes: "Good setup", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
            TradeTechnicalAnalysisTags: [], EmotionTags: null,
            ConfidenceLevel: TradingJournal.Shared.Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null, TradingSetupId: 99);

        _contextMock.Setup(c => c.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeScreenShots).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeScreenShot>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeEmotionTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeEmotionTag>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeHistoryChecklist).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistoryChecklist>().AsQueryable()).Object);
        _contextMock.Setup(c => c.TradeTechnicalAnalysisTags).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeTechnicalAnalysisTag>().AsQueryable()).Object);
        SetupAccessibleChecklists(42, 1);
        _setupProviderMock.Setup(x => x.HasSetupAsync(42, 99, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        SetupTradingProfiles();
        SetupTransactionalExecution();
        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        TradeHistory? createdTrade = null;
        _contextMock.Setup(c => c.TradeHistories.AddAsync(It.IsAny<TradeHistory>(), It.IsAny<CancellationToken>()))
            .Callback<TradeHistory, CancellationToken>((trade, _) => createdTrade = trade)
            .ReturnsAsync((TradeHistory trade, CancellationToken _) => null!);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(createdTrade);
        Assert.Equal(99, createdTrade!.TradingSetupId);
    }
}
