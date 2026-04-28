using Moq;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Features.V1.Watchlists;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Watchlists;

public class DeleteWatchlistHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly DeleteWatchlist.Handler _handler;

    public DeleteWatchlistHandlerTests()
    {
        _handler = new DeleteWatchlist.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Watchlist_Not_Found()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);

        var cmd = new DeleteWatchlist.Command { UserId = 1, WatchlistId = 999 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("WatchlistNotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Wrong_User()
    {
        var watchlists = new List<Watchlist> { new() { Id = 1, UserId = 2, Name = "Other" } };
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(watchlists).Object);

        var cmd = new DeleteWatchlist.Command { UserId = 1, WatchlistId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Soft_Deletes_Watchlist()
    {
        var watchlist = new Watchlist { Id = 1, UserId = 1, Name = "Test", IsDisabled = false };
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist> { watchlist }).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new DeleteWatchlist.Command { UserId = 1, WatchlistId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.True(watchlist.IsDisabled);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
