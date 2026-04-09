using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Moq;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.TradingSession;
using TradingJournal.Modules.Trades.Infrastructure;
using MockQueryable.Moq;
using TradingSessionEntity = TradingJournal.Modules.Trades.Domain.TradingSession;
using SharedEnums = TradingJournal.Shared.Common.Enum;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.TradingSession;

[TestFixture]
public class CreateTradeSessionValidatorTests
{
    private static readonly CreateTradeSession.Validator _validator = new();
    [Test] public void Should_Have_Error_When_FromTime_Is_Null() { var r = _validator.TestValidate(new CreateTradeSession.Request(null, 0)); r.ShouldHaveValidationErrorFor(x => x.FromTime); }
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new CreateTradeSession.Request(DateTime.UtcNow, 1)); r.ShouldNotHaveValidationErrorFor(x => x.FromTime); }
}

[TestFixture]
public class CreateTradeSessionHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private CreateTradeSession.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new CreateTradeSession.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_UserId_Is_Zero() { var result = await _handler.Handle(new CreateTradeSession.Request(DateTime.UtcNow, 0), CancellationToken.None); Assert.That(result.IsFailure, Is.True); }
    [Test] public async Task Handle_Returns_Success_When_Valid() { _dbMock.Setup(x => x.TradingSessions).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingSessionEntity>().AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new CreateTradeSession.Request(DateTime.UtcNow, 1), CancellationToken.None); Assert.That(result.IsSuccess, Is.True); }
}

[TestFixture]
public class GetTradeSessionsHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private GetTradeSessions.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new GetTradeSessions.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Sessions_When_Data_Exists() { var sessions = new List<TradingSessionEntity> { new() { Id = 1, FromTime = DateTime.UtcNow, Status = TradingJournal.Modules.Trades.Common.Enum.TradingSessionStatus.Active, CreatedBy = 1 } }.AsQueryable(); _dbMock.Setup(x => x.TradingSessions).Returns(DbSetMockHelper.CreateMockDbSet(sessions).Object); var result = await _handler.Handle(new GetTradeSessions.Request(1, 10, null, 1), CancellationToken.None); Assert.That(result.IsSuccess, Is.True); Assert.That(result.Value, Is.Not.Empty); }
    [Test] public async Task Handle_Returns_Empty_When_No_Data() { _dbMock.Setup(x => x.TradingSessions).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingSessionEntity>().AsQueryable()).Object); var result = await _handler.Handle(new GetTradeSessions.Request(1, 10, null), CancellationToken.None); Assert.That(result.IsSuccess, Is.True); Assert.That(result.Value, Is.Empty); }
}

[TestFixture]
public class EndTradeSessionValidatorTests
{
    private static readonly EndTradeSession.Validator _validator = new();
    [Test] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new EndTradeSession.Request(0, DateTime.UtcNow, null, null, 0)); r.ShouldHaveValidationErrorFor(x => x.Id); }
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new EndTradeSession.Request(1, DateTime.UtcNow, null, null, 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

[TestFixture]
public class EndTradeSessionHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private EndTradeSession.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new EndTradeSession.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { var dbSetMock = DbSetMockHelper.CreateMockDbSet(new List<TradingSessionEntity>().AsQueryable()); dbSetMock.Setup(x => x.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((TradingSessionEntity?)null); _dbMock.Setup(x => x.TradingSessions).Returns(dbSetMock.Object); _dbMock.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object); var result = await _handler.Handle(new EndTradeSession.Request(99, DateTime.UtcNow, null, null), CancellationToken.None); Assert.That(result.IsFailure, Is.True); }
    [Test] public async Task Handle_Returns_Success_When_Ended() { var session = new TradingSessionEntity { Id = 1, FromTime = DateTime.UtcNow, Status = TradingJournal.Modules.Trades.Common.Enum.TradingSessionStatus.Active, CreatedBy = 1 }; var dbSetMock = DbSetMockHelper.CreateMockDbSet(new List<TradingSessionEntity> { session }.AsQueryable()); dbSetMock.Setup(x => x.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(session); _dbMock.Setup(x => x.TradingSessions).Returns(dbSetMock.Object); _dbMock.Setup(x => x.TradeHistories).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradeHistory>().AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new EndTradeSession.Request(1, DateTime.UtcNow, null, null, 1), CancellationToken.None); Assert.That(result.IsSuccess, Is.True); Assert.That(session.Status, Is.EqualTo(TradingJournal.Modules.Trades.Common.Enum.TradingSessionStatus.Closed)); }
}

[TestFixture]
public class DeleteTradeSessionValidatorTests
{
    private static readonly DeleteTradeSession.Validator _validator = new();
    [Test] public void Should_Have_Error_When_Id_Is_Zero() { var r = _validator.TestValidate(new DeleteTradeSession.Request(0, 1)); r.ShouldHaveValidationErrorFor(x => x.Id); }
    [Test] public void Should_Not_Have_Error_When_Valid() { var r = _validator.TestValidate(new DeleteTradeSession.Request(1, 1)); r.ShouldNotHaveAnyValidationErrors(); }
}

[TestFixture]
public class DeleteTradeSessionHandlerTests
{
    private Mock<ITradeDbContext> _dbMock = null!;
    private DeleteTradeSession.Handler _handler = null!;
    [SetUp] public void SetUp() { _dbMock = new Mock<ITradeDbContext>(); _handler = new DeleteTradeSession.Handler(_dbMock.Object); }
    [Test] public async Task Handle_Returns_Failure_When_Not_Found() { _dbMock.Setup(x => x.TradingSessions).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingSessionEntity>().AsQueryable()).Object); var result = await _handler.Handle(new DeleteTradeSession.Request(99), CancellationToken.None); Assert.That(result.IsFailure, Is.True); }
    [Test] public async Task Handle_Returns_Success_When_Deleted() { var session = new TradingSessionEntity { Id = 1, FromTime = DateTime.UtcNow, CreatedBy = 1 }; _dbMock.Setup(x => x.TradingSessions).Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingSessionEntity> { session }.AsQueryable()).Object); _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1); var result = await _handler.Handle(new DeleteTradeSession.Request(1, 1), CancellationToken.None); Assert.That(result.IsSuccess, Is.True); _dbMock.Verify(x => x.TradingSessions.Remove(session), Times.Once); }
}
