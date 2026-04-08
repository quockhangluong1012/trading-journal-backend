//using FluentAssertions;
//using FluentValidation.TestHelper;
//using Microsoft.EntityFrameworkCore;
//using Moq;
//using TradingJournal.Modules.Trades.Domain;
//using TradingJournal.Modules.Trades.Features.V1.TradingSession;
//using TradingJournal.Modules.Trades.Infrastructure;

//namespace TradingJournal.Tests.Trades.Features.V1.TradingSession;

//[TestFixture]
//public class CreateTradeSessionValidatorTests
//{
//    private static readonly CreateTradeSession.Validator _validator = new();
//    [Test] public void Should_Have_Error_When_FromTime_Is_Null() { var r = _validator.TestValidate(new CreateTradeSession.Request(null, 0)); r.ShouldHaveValidationErrorFor(x => x.FromTime); }
//    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new CreateTradeSession.Request(DateTime.UtcNow, 1)); r.ShouldNotHaveValidationErrorFor(x => x.FromTime); }
//}

//[TestFixture]
//public class CreateTradeSessionHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private CreateTradeSession.Handler _handler = null!;
//    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new CreateTradeSession.Handler(_dbMock.Object); }
//    [Test] public async Task Handle_Returns_Failure_When_UserId_Is_Zero() { var result = await _handler.Handle(new CreateTradeSession.Request(DateTime.UtcNow, 0), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
//    [Test] public async Task Handle_Returns_Success_When_Valid() { _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new CreateTradeSession.Request(DateTime.UtcNow, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); }
//}

//[TestFixture]
//public class GetTradeSessionsHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private GetTradeSessions.Handler _handler = null!;
//    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetTradeSessions.Handler(_dbMock.Object); }
//    [Test] public async Task Handle_Returns_Sessions_When_Data_Exists() { var sessions = new List<TradingSession> { new() { Id = 1, FromTime = DateTime.UtcNow, Status = TradingSessionStatus.Open, CreatedBy = 1 } }.AsQueryable(); _dbMock.Setup(x => x.TradingSessions).Returns(sessions); var result = await _handler.Handle(new GetTradeSessions.Request(1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.Should().NotBeEmpty(); }
//    [Test] public async Task Handle_Returns_Empty_When_No_Data() { _dbMock.Setup(x => x.TradingSessions).Returns(new List<TradingSession>().AsQueryable()); var result = await _handler.Handle(new GetTradeSessions.Request(1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); result.Value.Should().BeEmpty(); }
//}

//[TestFixture]
//public class EndTradeSessionValidatorTests
//{
//    private static readonly EndTradeSession.Validator _validator = new();
//    [Test] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new EndTradeSession.Request(0, DateTime.UtcNow, null, null, 0)); r.ShouldHaveValidationErrorFor(x => x.Id); }
//    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new EndTradeSession.Request(1, DateTime.UtcNow, null, null, 1)); r.ShouldNotHaveAnyErrors(); }
//}

//[TestFixture]
//public class EndTradeSessionHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private EndTradeSession.Handler _handler = null!;
//    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new EndTradeSession.Handler(_dbMock.Object); }
//    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TradingSessions.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((TradingSession?)null); var result = await _handler.Handle(new EndTradeSession.Request(99, 1), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
//    [Test] public async Task Handle_Returns_Success_When_Ended() { var session = new TradingSession { Id = 1, FromTime = DateTime.UtcNow, Status = TradingSessionStatus.Open, CreatedBy = 1 }; _dbMock.Setup(x => x.TradingSessions.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(session); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new EndTradeSession.Request(1, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); session.Status.Should().Be(TradingSessionStatus.Closed); }
//}

//[TestFixture]
//public class DeleteTradeSessionValidatorTests
//{
//    private static readonly DeleteTradeSession.Validator _validator = new();
//    [Test] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new DeleteTradeSession.Request(0, 1)); r.ShouldHaveValidationErrorFor(x => x.Id); }
//    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new DeleteTradeSession.Request(1, 1)); r.ShouldNotHaveAnyErrors(); }
//}

//[TestFixture]
//public class DeleteTradeSessionHandlerTests
//{
//    private Mock<ITradeDbContext> _dbMock = null!;
//    private DeleteTradeSession.Handler _handler = null!;
//    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new DeleteTradeSession.Handler(_dbMock.Object); }
//    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TradingSessions.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((TradingSession?)null); var result = await _handler.Handle(new DeleteTradeSession.Request(99, 1), CancellationToken.None); result.IsFailure.Should().BeTrue(); }
//    [Test] public async Task Handle_Returns_Success_When_Deleted() { var session = new TradingSession { Id = 1, FromTime = DateTime.UtcNow, CreatedBy = 1 }; _dbMock.Setup(x => x.TradingSessions.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(session); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new DeleteTradeSession.Request(1, 1), CancellationToken.None); result.IsSuccess.Should().BeTrue(); _dbMock.Verify(x => x.TradingSessions.Remove(session), Times.Once); }
//}
