//using FluentAssertions;
//using FluentValidation.TestHelper;
//using Microsoft.EntityFrameworkCore;
//using Moq;
//using System.Text;
//using TradingJournal.Modules.Trades.Common.Enum;
//using TradingJournal.Modules.Trades.Domain;
//using TradingJournal.Modules.Trades.Features.V1.Review;
//using TradingJournal.Modules.Trades.Infrastructure;
//using TradingJournal.Modules.Trades.Services;
//using TradingJournal.Shared.Common.Enum;
//using TradingJournal.Shared.Dtos;
//using TradingJournal.Shared.Interfaces;

//namespace TradingJournal.Tests.Trades.Features.V1.Review;

//[TestFixture]
//public class SaveReviewValidatorTests
//{
//    private static readonly SaveReview.Validator _validator = new();
//    [Test] public void Should_Have_Error_When_PeriodType_Is_Invalid() { var r = _validator.TestValidate(new SaveReview.Request((ReviewPeriodType)99, DateTime.Now.AddDays(-7), DateTime.Now, null, 1)); r.ShouldHaveValidationErrorFor(x => x.PeriodType); }
//    [Test] public void Should_Have_Error_When_PeriodStart_After_PeriodEnd() { var r = _validator.TestValidate(new SaveReview.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(1), DateTime.Now, null, 1)); r.ShouldHaveValidationErrorFor(x => x.PeriodStart); }
//    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new SaveReview.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(-7), DateTime.Now, "notes", 1)); r.ShouldNotHaveAnyErrors(); }
//}

//[TestFixture]
//public class SaveReviewHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private Mock<ITradeProvider> _tradeProviderMock = null!;
//    private SaveReview.Handler _handler = null!;
//    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _tradeProviderMock = new Mock<ITradeProvider>(); _handler = new SaveReview.Handler(_dbMock.Object, _tradeProviderMock.Object); }
//    [Test] public async Task Handle_Creates_New_Review_When_None_Exists() { var trades = new List<TradeCacheDto> { new() { Id = 1, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, Pnl = 50m, StopLoss = 1.095, TargetTier1 = 1.12, Status = TradeStatus.Closed, Date = DateTime.Now.AddDays(-1), ClosedDate = DateTime.Now, CreatedBy = 1 } }; _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades); _dbMock.Setup(x => x.TradingReviews).Returns(new List<TradingReview>().AsQueryable()); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new SaveReview.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(-7), DateTime.Now, null, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); _dbMock.Verify(x => x.TradingReviews.AddAsync(It.IsAny<TradingReview>(), It.IsAny<CancellationToken>()), Times.Once); }
//}

//[TestFixture]
//public class GetReviewValidatorTests
//{
//    private static readonly GetReview.Validator _validator = new();
//    [Test] public void Should_Have_Error_When_PeriodType_Is_Invalid() { var r = _validator.TestValidate(new GetReview.Request((ReviewPeriodType)99, DateTime.Now)); r.ShouldHaveValidationErrorFor(x => x.PeriodType); }
//    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new GetReview.Request(ReviewPeriodType.Weekly, DateTime.Now, 1)); r.ShouldNotHaveAnyErrors(); }
//}

//[TestFixture]
//public class GetReviewHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private Mock<ITradeProvider> _tradeProviderMock = null!;
//    private Mock<IPromptService> _promptServiceMock = null!;
//    private GetReview.Handler _handler = null!;
//    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _tradeProviderMock = new Mock<ITradeProvider>(); _promptServiceMock = new Mock<IPromptService>(); _handler = new GetReview.Handler(_dbMock.Object, _tradeProviderMock.Object, _promptServiceMock.Object); }
//    [Test] public async Task Handle_Returns_Empty_When_No_Data() { _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>()); _dbMock.Setup(x => x.TradingReviews).Returns(new List<TradingReview>().AsQueryable()); var result = await _handler.Handle(new GetReview.Request(ReviewPeriodType.Weekly, DateTime.Now, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); }
//}



//[TestFixture]
//public class GetReviewSummaryStatusHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private GetReviewSummaryStatus.Handler _handler = null!;
//    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetReviewSummaryStatus.Handler(_dbMock.Object); }
//    [Test] public async Task Handle_Returns_Empty_When_No_Reviews() { _dbMock.Setup(x => x.TradingReviews).Returns(new List<TradingReview>().AsQueryable()); var result = await _handler.Handle(new GetReviewSummaryStatus.Request(1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.Should().BeEmpty(); }
//    [Test] public async Task Handle_Returns_Reviews_For_UserId() { var reviews = new List<TradingReview> { new() { Id = 1, PeriodType = ReviewPeriodType.Weekly, PeriodStart = DateTime.Now.AddDays(-7), CreatedBy = 1 } }.AsQueryable(); _dbMock.Setup(x => x.TradingReviews).Returns(reviews); var result = await _handler.Handle(new GetReviewSummaryStatus.Request(1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.Should().HaveCount(1); }
//}

//[TestFixture]
//public class GetReviewTradesValidatorTests
//{
//    private static readonly GetReviewTrades.Validator _validator = new();
//    [Test] public void Should_Have_Error_When_FromDate_After_ToDate() { var r = _validator.TestValidate(new GetReviewTrades.Request(DateTime.Now.AddDays(1), DateTime.Now)); r.ShouldHaveValidationErrorFor(x => x.FromDate); }
//    [Test] public void Should_Have_Error_When_Page_Is_Zero() { var r = _validator.TestValidate(new GetReviewTrades.Request(DateTime.Now.AddDays(-7), DateTime.Now, 0, 50)); r.ShouldHaveValidationErrorFor(x => x.Page); }
//    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new GetReviewTrades.Request(DateTime.Now.AddDays(-7), DateTime.Now, 1, 10, 1)); r.ShouldNotHaveAnyErrors(); }
//}

//[TestFixture]
//public class GetReviewTradesHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private GetReviewTrades.Handler _handler = null!;
//    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetReviewTrades.Handler(_dbMock.Object); }
//    [Test] public async Task Handle_Returns_Empty_When_No_Data() { _dbMock.Setup(x => x.TradeHistories).Returns(new List<TradeHistory>().AsQueryable()); var result = await _handler.Handle(new GetReviewTrades.Request(DateTime.Now.AddDays(-30), DateTime.Now, UserId: 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.TotalItems.Should().Be(0); }
//}

//[TestFixture]
//public class GenerateReviewSummaryValidatorTests
//{
//    private static readonly GenerateReviewSummary.Validator _validator = new();
//    [Test] public void Should_Have_Error_When_PeriodType_Is_Invalid() { var r = _validator.TestValidate(new GenerateReviewSummary.Request((ReviewPeriodType)99, DateTime.Now.AddDays(-7))); r.ShouldHaveValidationErrorFor(x => x.PeriodType); }
//    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new GenerateReviewSummary.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(-7))); r.ShouldNotHaveAnyErrors(); }
//}

//[TestFixture]
//public class GenerateReviewSummaryHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private Mock<IPromptService> _promptServiceMock = null!;
//    private GenerateReviewSummary.Handler _handler = null!;
//    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _promptServiceMock = new Mock<IPromptService>(); _handler = new GenerateReviewSummary.Handler(_dbMock.Object, _promptServiceMock.Object); }
//    [Test] public async Task Handle_Returns_Failure_When_No_Trades_In_Period() { _dbMock.Setup(x => x.TradingReviews).Returns(new List<TradingReview>().AsQueryable()); _promptServiceMock.Setup(x => x.GenerateReviewPrompt(It.IsAny<IList<TradeHistory>>(), It.IsAny<ReviewPeriodType>(), It.IsAny<ReviewPeriodType>(), It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty); var result = await _handler.Handle(new GenerateReviewSummary.Request(ReviewPeriodType.Weekly, DateTime.Now.AddDays(-7), 1), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
//}
