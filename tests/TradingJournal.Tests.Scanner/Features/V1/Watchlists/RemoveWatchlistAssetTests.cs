using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Features.V1.Watchlists;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Watchlists;

public class RemoveWatchlistAssetValidatorTests
{
    private static readonly RemoveWatchlistAsset.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_WatchlistId_Is_Zero()
    {
        var cmd = new RemoveWatchlistAsset.Command { WatchlistId = 0, AssetId = 1 };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.WatchlistId);
    }

    [Fact]
    public void Should_Have_Error_When_AssetId_Is_Zero()
    {
        var cmd = new RemoveWatchlistAsset.Command { WatchlistId = 1, AssetId = 0 };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.AssetId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var cmd = new RemoveWatchlistAsset.Command { WatchlistId = 1, AssetId = 1 };
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}

public class RemoveWatchlistAssetHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly RemoveWatchlistAsset.Handler _handler;

    public RemoveWatchlistAssetHandlerTests()
    {
        _handler = new RemoveWatchlistAsset.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Watchlist_Not_Found()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);

        var cmd = new RemoveWatchlistAsset.Command { UserId = 1, WatchlistId = 999, AssetId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("WatchlistNotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Asset_Not_Found()
    {
        var watchlists = new List<Watchlist> { new() { Id = 1, UserId = 1, Name = "Test" } };
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(watchlists).Object);
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset>()).Object);

        var cmd = new RemoveWatchlistAsset.Command { UserId = 1, WatchlistId = 1, AssetId = 999 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AssetNotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Asset_Removed()
    {
        var watchlists = new List<Watchlist> { new() { Id = 1, UserId = 1, Name = "Test" } };
        var assets = new List<WatchlistAsset> { new() { Id = 1, WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple" } };

        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(watchlists).Object);
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(assets).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new RemoveWatchlistAsset.Command { UserId = 1, WatchlistId = 1, AssetId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Asset_In_Different_Watchlist()
    {
        var watchlists = new List<Watchlist> { new() { Id = 1, UserId = 1, Name = "Test" } };
        var assets = new List<WatchlistAsset> { new() { Id = 1, WatchlistId = 2, Symbol = "AAPL", DisplayName = "Apple" } };

        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(watchlists).Object);
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(assets).Object);

        var cmd = new RemoveWatchlistAsset.Command { UserId = 1, WatchlistId = 1, AssetId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
