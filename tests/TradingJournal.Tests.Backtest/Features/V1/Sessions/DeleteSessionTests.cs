using Moq;
using TradingJournal.Modules.Backtest.Common.Enums;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Features.V1.Sessions;
using TradingJournal.Modules.Backtest.Infrastructure;
using TradingJournal.Tests.Backtest.Helpers;

namespace TradingJournal.Tests.Backtest.Features.V1.Sessions;

public sealed class DeleteSessionHandlerTests
{
    private readonly Mock<IBacktestDbContext> _context = new();
    private readonly DeleteSession.Handler _handler;

    public DeleteSessionHandlerTests()
    {
        _handler = new DeleteSession.Handler(_context.Object);
    }

    [Fact]
    public async Task Handle_SoftDeletes_WhenSessionFound()
    {
        var session = new BacktestSession
        {
            Id = 10,
            CreatedBy = 42,
            Status = BacktestSessionStatus.InProgress,
            IsDisabled = false
        };

        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession> { session }.AsQueryable()).Object);
        _context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(
            new DeleteSession.Request(10) { UserId = 42 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(session.IsDisabled);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenSessionNotFound()
    {
        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession>().AsQueryable()).Object);

        var result = await _handler.Handle(
            new DeleteSession.Request(999) { UserId = 42 }, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenWrongUser()
    {
        var session = new BacktestSession
        {
            Id = 10,
            CreatedBy = 42,
            Status = BacktestSessionStatus.InProgress,
            IsDisabled = false
        };

        _context.Setup(x => x.BacktestSessions)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<BacktestSession> { session }.AsQueryable()).Object);

        var result = await _handler.Handle(
            new DeleteSession.Request(10) { UserId = 99 }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(session.IsDisabled);
    }
}
