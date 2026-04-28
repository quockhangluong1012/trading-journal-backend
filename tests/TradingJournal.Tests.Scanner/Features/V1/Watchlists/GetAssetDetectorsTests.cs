using Moq;
using TradingJournal.Modules.Scanner.Common.Enums;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Features.V1.Watchlists;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Watchlists;

public class GetAssetDetectorsHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly GetAssetDetectors.Handler _handler;

    public GetAssetDetectorsHandlerTests()
    {
        _handler = new GetAssetDetectors.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Asset_Not_Found()
    {
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset>()).Object);

        var req = new GetAssetDetectors.Request { UserId = 1, WatchlistId = 1, AssetId = 999 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AssetNotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Returns_Success_With_Enabled_Detectors()
    {
        var watchlist = new Watchlist { Id = 1, UserId = 1, Name = "Test" };
        var asset = new WatchlistAsset
        {
            Id = 1, WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple",
            Watchlist = watchlist,
            EnabledDetectors = new List<WatchlistAssetDetector>
            {
                new() { Id = 1, WatchlistAssetId = 1, PatternType = IctPatternType.FVG, IsEnabled = true },
                new() { Id = 2, WatchlistAssetId = 1, PatternType = IctPatternType.OrderBlock, IsEnabled = false },
                new() { Id = 3, WatchlistAssetId = 1, PatternType = IctPatternType.Liquidity, IsEnabled = true }
            }
        };
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset> { asset }).Object);

        var req = new GetAssetDetectors.Request { UserId = 1, WatchlistId = 1, AssetId = 1 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AAPL", result.Value.Symbol);
        // Only enabled detectors should be returned
        Assert.Equal(2, result.Value.EnabledDetectors.Count);
        Assert.Contains("FVG", result.Value.EnabledDetectors);
        Assert.Contains("Liquidity", result.Value.EnabledDetectors);
        Assert.DoesNotContain("OrderBlock", result.Value.EnabledDetectors);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Asset_Is_Disabled()
    {
        var watchlist = new Watchlist { Id = 1, UserId = 1, Name = "Test" };
        var asset = new WatchlistAsset
        {
            Id = 1, WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple",
            Watchlist = watchlist, IsDisabled = true,
            EnabledDetectors = new List<WatchlistAssetDetector>()
        };
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset> { asset }).Object);

        var req = new GetAssetDetectors.Request { UserId = 1, WatchlistId = 1, AssetId = 1 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Wrong_User()
    {
        var watchlist = new Watchlist { Id = 1, UserId = 2, Name = "Other" };
        var asset = new WatchlistAsset
        {
            Id = 1, WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple",
            Watchlist = watchlist,
            EnabledDetectors = new List<WatchlistAssetDetector>()
        };
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset> { asset }).Object);

        var req = new GetAssetDetectors.Request { UserId = 1, WatchlistId = 1, AssetId = 1 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
