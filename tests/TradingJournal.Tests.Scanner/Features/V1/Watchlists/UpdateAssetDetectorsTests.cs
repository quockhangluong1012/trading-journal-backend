using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Scanner.Common.Enums;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Features.V1.Watchlists;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Watchlists;

public class UpdateAssetDetectorsValidatorTests
{
    private static readonly UpdateAssetDetectors.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_WatchlistId_Is_Zero()
    {
        var cmd = new UpdateAssetDetectors.Command { WatchlistId = 0, AssetId = 1 };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.WatchlistId);
    }

    [Fact]
    public void Should_Have_Error_When_AssetId_Is_Zero()
    {
        var cmd = new UpdateAssetDetectors.Command { WatchlistId = 1, AssetId = 0 };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.AssetId);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var cmd = new UpdateAssetDetectors.Command { WatchlistId = 1, AssetId = 1 };
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}

public class UpdateAssetDetectorsHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly UpdateAssetDetectors.Handler _handler;

    public UpdateAssetDetectorsHandlerTests()
    {
        _handler = new UpdateAssetDetectors.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Asset_Not_Found()
    {
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset>()).Object);

        var cmd = new UpdateAssetDetectors.Command
        {
            UserId = 1, WatchlistId = 1, AssetId = 999,
            EnabledPatterns = ["FVG"]
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AssetNotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Returns_Success_With_Valid_Patterns()
    {
        var watchlist = new Watchlist { Id = 1, UserId = 1, Name = "Test" };
        var asset = new WatchlistAsset
        {
            Id = 1, WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple",
            Watchlist = watchlist, EnabledDetectors = new List<WatchlistAssetDetector>()
        };
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset> { asset }).Object);
        _dbMock.Setup(x => x.WatchlistAssetDetectors).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAssetDetector>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new UpdateAssetDetectors.Command
        {
            UserId = 1, WatchlistId = 1, AssetId = 1,
            EnabledPatterns = ["FVG", "OrderBlock"]
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.EnabledDetectors.Count);
        Assert.Contains("FVG", result.Value.EnabledDetectors);
        Assert.Contains("OrderBlock", result.Value.EnabledDetectors);
    }

    [Fact]
    public async Task Handle_Ignores_Invalid_Pattern_Strings()
    {
        var watchlist = new Watchlist { Id = 1, UserId = 1, Name = "Test" };
        var asset = new WatchlistAsset
        {
            Id = 1, WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple",
            Watchlist = watchlist, EnabledDetectors = new List<WatchlistAssetDetector>()
        };
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset> { asset }).Object);
        _dbMock.Setup(x => x.WatchlistAssetDetectors).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAssetDetector>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new UpdateAssetDetectors.Command
        {
            UserId = 1, WatchlistId = 1, AssetId = 1,
            EnabledPatterns = ["FVG", "InvalidPattern", "OrderBlock"]
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.EnabledDetectors.Count);
    }

    [Fact]
    public async Task Handle_Clears_All_Detectors_When_Empty_List()
    {
        var watchlist = new Watchlist { Id = 1, UserId = 1, Name = "Test" };
        var asset = new WatchlistAsset
        {
            Id = 1, WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple",
            Watchlist = watchlist,
            EnabledDetectors = new List<WatchlistAssetDetector>
            {
                new() { Id = 1, WatchlistAssetId = 1, PatternType = IctPatternType.FVG, IsEnabled = true }
            }
        };
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset> { asset }).Object);
        _dbMock.Setup(x => x.WatchlistAssetDetectors).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAssetDetector>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new UpdateAssetDetectors.Command
        {
            UserId = 1, WatchlistId = 1, AssetId = 1,
            EnabledPatterns = []
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.EnabledDetectors);
    }
}
