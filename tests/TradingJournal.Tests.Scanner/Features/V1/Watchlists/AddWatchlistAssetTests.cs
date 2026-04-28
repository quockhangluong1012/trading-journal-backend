using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Features.V1.Watchlists;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Watchlists;

public class AddWatchlistAssetValidatorTests
{
    private static readonly AddWatchlistAsset.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_WatchlistId_Is_Zero()
    {
        var cmd = new AddWatchlistAsset.Command { WatchlistId = 0, Symbol = "AAPL", DisplayName = "Apple" };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.WatchlistId);
    }

    [Fact]
    public void Should_Have_Error_When_Symbol_Is_Empty()
    {
        var cmd = new AddWatchlistAsset.Command { WatchlistId = 1, Symbol = "", DisplayName = "Apple" };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Symbol);
    }

    [Fact]
    public void Should_Have_Error_When_Symbol_Exceeds_MaxLength()
    {
        var cmd = new AddWatchlistAsset.Command { WatchlistId = 1, Symbol = new string('X', 31), DisplayName = "Apple" };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Symbol);
    }

    [Fact]
    public void Should_Have_Error_When_DisplayName_Is_Empty()
    {
        var cmd = new AddWatchlistAsset.Command { WatchlistId = 1, Symbol = "AAPL", DisplayName = "" };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Should_Have_Error_When_DisplayName_Exceeds_MaxLength()
    {
        var cmd = new AddWatchlistAsset.Command { WatchlistId = 1, Symbol = "AAPL", DisplayName = new string('A', 101) };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var cmd = new AddWatchlistAsset.Command { WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple Inc." };
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}

public class AddWatchlistAssetHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly AddWatchlistAsset.Handler _handler;

    public AddWatchlistAssetHandlerTests()
    {
        _handler = new AddWatchlistAsset.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Watchlist_Not_Found()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);

        var cmd = new AddWatchlistAsset.Command { UserId = 1, WatchlistId = 999, Symbol = "AAPL", DisplayName = "Apple" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("WatchlistNotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Watchlist_Belongs_To_Different_User()
    {
        var watchlists = new List<Watchlist> { new() { Id = 1, UserId = 2, Name = "Other" } };
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(watchlists).Object);

        var cmd = new AddWatchlistAsset.Command { UserId = 1, WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Duplicate_Symbol()
    {
        var watchlists = new List<Watchlist> { new() { Id = 1, UserId = 1, Name = "Test" } };
        var assets = new List<WatchlistAsset> { new() { Id = 1, WatchlistId = 1, Symbol = "AAPL", DisplayName = "Apple" } };

        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(watchlists).Object);
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(assets).Object);

        var cmd = new AddWatchlistAsset.Command { UserId = 1, WatchlistId = 1, Symbol = "aapl", DisplayName = "Apple" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("DuplicateAsset", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Returns_Success_And_Uppercases_Symbol()
    {
        var watchlists = new List<Watchlist> { new() { Id = 1, UserId = 1, Name = "Test" } };
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(watchlists).Object);
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new AddWatchlistAsset.Command { UserId = 1, WatchlistId = 1, Symbol = "msft", DisplayName = "Microsoft" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("MSFT", result.Value.Symbol);
        Assert.Equal("Microsoft", result.Value.DisplayName);
        Assert.Empty(result.Value.EnabledDetectors);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
