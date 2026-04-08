//using NUnit.Framework;
//using FluentAssertions;
//using Moq;
//using TradingJournal.Modules.Trades.Features.V1.Trade;
//using TradingJournal.Modules.Trades.Infrastructure;
//using TradingJournal.Modules.Trades.Domain;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.AspNetCore.Http;
//using SharedEnums = TradingJournal.Shared.Common.Enum;

//namespace TradingJournal.Tests.Trades.Features.V1.Trade;

//[TestFixture]
//public sealed class CreateTradeValidatorTests
//{
//    private CreateTrade.Validator _validator = null!;

//    [SetUp]
//    public void SetUp()
//    {
//        _validator = new CreateTrade.Validator();
//    }

//    private CreateTrade.Request CreateValidRequest() =>
//        new(
//            Asset: "EURUSD",
//            Position: SharedEnums.PositionType.Long,
//            EntryPrice: 1.0850,
//            TargetTier1: 1.0900,
//            TargetTier2: null,
//            TargetTier3: null,
//            StopLoss: 1.0800,
//            Notes: "Good setup",
//            Date: DateTime.UtcNow,
//            Status: SharedEnums.TradeStatus.Open,
//            ExitPrice: null,
//            Pnl: null,
//            ClosedDate: null,
//            Screenshots: [],
//            TradeTechnicalAnalysisTags: [],
//            EmotionTags: [],
//            ConfidenceLevel: Common.Enum.ConfidenceLevel.Neutral,
//            PsychologyNotes: null,
//            TradeHistoryChecklists: [1],
//            TradingZoneId: 1,
//            TradingSessionId: null);

//    [Test]
//    public void Validate_ValidRequest_ReturnsValid()
//    {
//        var request = CreateValidRequest();
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeTrue();
//    }

//    [Test]
//    public void Validate_NullAsset_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { Asset = null! };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Asset"));
//    }

//    [Test]
//    public void Validate_InvalidPosition_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { Position = (SharedEnums.PositionType)99 };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Position"));
//    }

//    [Test]
//    public void Validate_EntryPriceZero_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { EntryPrice = 0 };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("EntryPrice"));
//    }

//    [Test]
//    public void Validate_EntryPriceNegative_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { EntryPrice = -1 };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//    }

//    [Test]
//    public void Validate_TargetTier1Zero_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { TargetTier1 = 0 };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Target Tier"));
//    }

//    [Test]
//    public void Validate_StopLossZero_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { StopLoss = 0 };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Stop Loss"));
//    }

//    [Test]
//    public void Validate_NullNotes_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { Notes = null! };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("notes"));
//    }

//    [Test]
//    public void Validate_EmptyCheckoutLists_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { TradeHistoryChecklists = [] };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("checklist"));
//    }

//    [Test]
//    public void Validate_TradingZoneIdZero_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { TradingZoneId = 0 };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Trading Zone"));
//    }

//    [Test]
//    public void Validate_InvalidStatus_ReturnsInvalid()
//    {
//        var request = CreateValidRequest() with { Status = (SharedEnums.TradeStatus)99 };
//        var result = _validator.Validate(request);
//        result.IsValid.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Status"));
//    }
//}

//[TestFixture]
//public sealed class CreateTradeHandlerTests
//{
//    private Mock<ITradeDbContext> _contextMock = null!;
//    private Mock<IWebHostEnvironment> _envMock = null!;
//    private Mock<IHttpContextAccessor> _httpContextAccessorMock = null!;
//    private CreateTrade.Handler _handler = null!;

//    [SetUp]
//    public void SetUp()
//    {
//        _contextMock = new Mock<ITradeDbContext>(MockBehavior.Strict);
//        _envMock = new Mock<IWebHostEnvironment>(MockBehavior.Strict);
//        _httpContextAccessorMock = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
//        _handler = new CreateTrade.Handler(_contextMock.Object, _envMock.Object, _httpContextAccessorMock.Object);
//    }

//    [Test]
//    public async Task Handle_ValidRequest_CreatesTradeAndReturnsSuccess()
//    {
//        var request = new CreateTrade.Request(
//            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850,
//            TargetTier1: 1.0900, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800,
//            Notes: "Good setup", Date: DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
//            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
//            TradeTechnicalAnalysisTags: [], EmotionTags: null,
//            ConfidenceLevel: Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
//            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null);

//        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
//        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
//        _contextMock.Setup(c => c.CommitTransaction()).Returns(Task.CompletedTask);

//        var result = await _handler.Handle(request, CancellationToken.None);

//        result.IsSuccess.Should().BeTrue();
//        result.Value.Should().BeGreaterThan(0);
//        _contextMock.Verify(c => c.BeginTransaction(), Times.Once);
//        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
//        _contextMock.Verify(c => c.CommitTransaction(), Times.Once);
//    }

//    [Test]
//    public async Task Handle_SaveChangesReturnsZero_ReturnsFailure()
//    {
//        var request = new CreateTrade.Request(
//            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850,
//            TargetTier1: 1.0900, TargetTier2: null, TargetTier3: null, StopLoss: 1.0800,
//            Notes: "Test", DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
//            ExitPrice: null, Pnl: null, ClosedDate: null, Screenshots: [],
//            TradeTechnicalAnalysisTags: [], EmotionTags: null,
//            ConfidenceLevel: Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
//            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null);

//        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
//        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
//        _contextMock.Setup(c => c.RollbackTransaction()).Returns(Task.CompletedTask);

//        var result = await _handler.Handle(request, CancellationToken.None);

//        result.IsSuccess.Should().BeFalse();
//        _contextMock.Verify(c => c.RollbackTransaction(), Times.Once);
//    }

//    [Test]
//    public async Task Handle_ExceptionOccurs_RollbacksAndReturnsFailure()
//    {
//        var request = new CreateTrade.Request(
//            Asset: "EURUSD", Position: SharedEnums.PositionType.Long, EntryPrice: 1.0850,
//            TargetTier1: 1.0900, null, null, StopLoss: 1.0800,
//            Notes: "Test", DateTime.UtcNow, Status: SharedEnums.TradeStatus.Open,
//            null, null, null, Screenshots: [],
//            TradeTechnicalAnalysisTags: [], EmotionTags: null,
//            ConfidenceLevel: Common.Enum.ConfidenceLevel.Neutral, PsychologyNotes: null,
//            TradeHistoryChecklists: [1], TradingZoneId: 1, TradingSessionId: null);

//        _contextMock.Setup(c => c.BeginTransaction()).Returns(Task.CompletedTask);
//        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
//            .ThrowsAsync(new InvalidOperationException("DB error"));
//        _contextMock.Setup(c => c.RollbackTransaction()).Returns(Task.CompletedTask);

//        var result = await _handler.Handle(request, CancellationToken.None);

//        result.IsSuccess.Should().BeFalse();
//        result.Errors.Should().Contain(e => e.Description.Contains("DB error"));
//        _contextMock.Verify(c => c.RollbackTransaction(), Times.Once);
//    }
//}
