using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Text;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.Review;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Modules.Trades.Services;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;
using TradingJournal.Tests.Trades.Helpers;
using TradingJournal.Messaging.Shared.Abstractions;

namespace TradingJournal.Tests.Trades.Features.V1.Review;

public class SaveReviewValidatorTests
{
    private static readonly SaveReview.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_PeriodType_Is_Invalid() { var r = _validator.TestValidate(new SaveReview.Request((ReviewPeriodType)99, DateTime.Now.AddDays(-7), DateTime.Now, null, 1)); r.ShouldHaveValidationErrorFor(x => x.PeriodType); }
    [Fact] public void Should_Have_Error_When_PeriodStart_After_PeriodEnd() { var r = _validator.TestValidate(new SaveReview.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(1), DateTime.Now, null, 1)); r.ShouldHaveValidationErrorFor(x => x.PeriodStart); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new SaveReview.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(-7), DateTime.Now, "notes", 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class SaveReviewHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private SaveReview.Handler _handler = null!;
    public SaveReviewHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _tradeProviderMock = new Mock<ITradeProvider>(); _handler = new SaveReview.Handler(_dbMock.Object, _tradeProviderMock.Object); }
    [Fact] public async Task Handle_Creates_New_Review_When_None_Exists() { var trades = new List<TradeCacheDto> { new() { Id = 1, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, Pnl = 50m, StopLoss = 1.095, TargetTier1 = 1.12, Status = TradeStatus.Closed, Date = DateTime.Now.AddDays(-1), ClosedDate = DateTime.Now, CreatedBy = 1 } }; _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades); _dbMock.Setup(x => x.TradingReviews).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingReview>().AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new SaveReview.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(-7), DateTime.Now, null, 1), CancellationToken.None); Assert.True(result.IsSuccess); _dbMock.Verify(x => x.TradingReviews.AddAsync(It.IsAny<TradingReview>(), It.IsAny<CancellationToken>()), Times.Once); }
}

public class GetReviewValidatorTests
{
    private static readonly GetReview.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_PeriodType_Is_Invalid() { var r = _validator.TestValidate(new GetReview.Request((ReviewPeriodType)99, DateTime.Now)); r.ShouldHaveValidationErrorFor(x => x.PeriodType); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new GetReview.Request(ReviewPeriodType.Weekly, DateTime.Now, 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class GetReviewHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private Mock<IPromptService> _promptServiceMock = null!;
    private GetReview.Handler _handler = null!;
    public GetReviewHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _tradeProviderMock = new Mock<ITradeProvider>(); _promptServiceMock = new Mock<IPromptService>(); _handler = new GetReview.Handler(_dbMock.Object, _tradeProviderMock.Object); }
    [Fact] public async Task Handle_Returns_Empty_When_No_Data() { _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>()); _dbMock.Setup(x => x.TradingReviews).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingReview>().AsQueryable()).Object); var result = await _handler.Handle(new GetReview.Request(ReviewPeriodType.Weekly, DateTime.Now, 1), CancellationToken.None); Assert.True(result.IsSuccess); }
}



public class GetReviewSummaryStatusHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetReviewSummaryStatus.Handler _handler = null!;
    public GetReviewSummaryStatusHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetReviewSummaryStatus.Handler(_dbMock.Object); }
    [Fact] public async Task Handle_Returns_Empty_When_No_Reviews() { _dbMock.Setup(x => x.TradingReviews).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingReview>().AsQueryable()).Object); var result = await _handler.Handle(new GetReviewSummaryStatus.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(-7), 1), CancellationToken.None); Assert.True(result.IsSuccess); Assert.False(result.Value.IsGenerating); }
    [Fact] public async Task Handle_Returns_Status_For_UserId() { var dt = DateTime.Now.AddDays(-14).Date; var reviews = new List<TradingReview> { new() { Id = 1, PeriodType = ReviewPeriodType.Weekly, PeriodStart = dt, CreatedBy = 1, AiSummaryGenerating = true } }.AsQueryable(); _dbMock.Setup(x => x.TradingReviews).Returns(DbSetMockHelper.CreateMockDbSet(reviews).Object); var result = await _handler.Handle(new GetReviewSummaryStatus.Request(ReviewPeriodType.Weekly, dt, 1), CancellationToken.None); Assert.True(result.IsSuccess); Assert.True(result.Value.IsGenerating); }
}

public class GetReviewTradesValidatorTests
{
    private static readonly GetReviewTrades.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_FromDate_After_ToDate() { var r = _validator.TestValidate(new GetReviewTrades.Request(DateTime.Now.AddDays(1), DateTime.Now)); r.ShouldHaveValidationErrorFor(x => x.FromDate); }
    [Fact] public void Should_Have_Error_When_Page_Is_Zero() { var r = _validator.TestValidate(new GetReviewTrades.Request(DateTime.Now.AddDays(-7), DateTime.Now, 0, 50)); r.ShouldHaveValidationErrorFor(x => x.Page); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new GetReviewTrades.Request(DateTime.Now.AddDays(-7), DateTime.Now, 1, 10, 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class GetReviewTradesHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetReviewTrades.Handler _handler = null!;
    public GetReviewTradesHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetReviewTrades.Handler(_dbMock.Object); }
    [Fact] public async Task Handle_Returns_Empty_When_No_Data() { _dbMock.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object); var result = await _handler.Handle(new GetReviewTrades.Request(DateTime.Now.AddDays(-30), DateTime.Now, UserId: 1), CancellationToken.None); Assert.True(result.IsSuccess); Assert.Equal(0, result.Value.TotalItems); }
}

public class GenerateReviewSummaryValidatorTests
{
    private static readonly GenerateReviewSummary.Validator _validator = new();
    [Fact] public void Should_Have_Error_When_PeriodType_Is_Invalid() { var r = _validator.TestValidate(new GenerateReviewSummary.Request((ReviewPeriodType)99, DateTime.Now.AddDays(-7), DateTime.Now)); r.ShouldHaveValidationErrorFor(x => x.PeriodType); }
    [Fact] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new GenerateReviewSummary.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(-7), DateTime.Now)); r.ShouldNotHaveAnyValidationErrors(); }
}

public class GenerateReviewSummaryHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private Mock<IEventBus> _eventBusMock = null!;
    private GenerateReviewSummary.Handler _handler = null!;
    public GenerateReviewSummaryHandlerTests() { _dbMock = new Mock<ITradeDbContext>(); _eventBusMock = new Mock<IEventBus>(); _handler = new GenerateReviewSummary.Handler(_dbMock.Object, _eventBusMock.Object); }
    [Fact] public async Task Handle_Returns_Success_When_Valid() { _dbMock.Setup(x => x.TradingReviews).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingReview>().AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new GenerateReviewSummary.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(-7), DateTime.Now, 1), CancellationToken.None); Assert.True(result.IsSuccess); }
}
