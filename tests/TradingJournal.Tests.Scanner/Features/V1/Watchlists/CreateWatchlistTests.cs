using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Dto;
using TradingJournal.Modules.Scanner.Features.V1.Watchlists;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Watchlists;

public class CreateWatchlistValidatorTests
{
    private static readonly CreateWatchlist.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var command = new CreateWatchlist.Command { Name = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Is_Null()
    {
        var command = new CreateWatchlist.Command { Name = null! };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Exceeds_MaxLength()
    {
        var command = new CreateWatchlist.Command { Name = new string('A', 101) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Name_Is_Valid()
    {
        var command = new CreateWatchlist.Command { Name = "My Watchlist" };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Asset_Symbol_Is_Empty()
    {
        var command = new CreateWatchlist.Command
        {
            Name = "Valid",
            Assets = [new CreateWatchlistAssetRequest("", "Display")]
        };
        var result = _validator.TestValidate(command);
        Assert.True(result.Errors.Count > 0);
    }

    [Fact]
    public void Should_Have_Error_When_Asset_DisplayName_Is_Empty()
    {
        var command = new CreateWatchlist.Command
        {
            Name = "Valid",
            Assets = [new CreateWatchlistAssetRequest("AAPL", "")]
        };
        var result = _validator.TestValidate(command);
        Assert.True(result.Errors.Count > 0);
    }

    [Fact]
    public void Should_Have_Error_When_Asset_Symbol_Exceeds_MaxLength()
    {
        var command = new CreateWatchlist.Command
        {
            Name = "Valid",
            Assets = [new CreateWatchlistAssetRequest(new string('X', 31), "Display")]
        };
        var result = _validator.TestValidate(command);
        Assert.True(result.Errors.Count > 0);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Assets_Are_Valid()
    {
        var command = new CreateWatchlist.Command
        {
            Name = "Valid",
            Assets = [new CreateWatchlistAssetRequest("AAPL", "Apple Inc.")]
        };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Not_Have_Error_When_Assets_Are_Empty()
    {
        var command = new CreateWatchlist.Command
        {
            Name = "Valid",
            Assets = []
        };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class CreateWatchlistHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock;
    private readonly CreateWatchlist.Handler _handler;

    public CreateWatchlistHandlerTests()
    {
        _dbMock = new Mock<IScannerDbContext>();
        _handler = new CreateWatchlist.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Valid()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateWatchlist.Command
        {
            UserId = 1,
            Name = "ICT Watchlist",
            Assets = [new CreateWatchlistAssetRequest("AAPL", "Apple Inc.")]
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ICT Watchlist", result.Value.Name);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task Handle_Converts_Symbol_To_UpperCase()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateWatchlist.Command
        {
            UserId = 1,
            Name = "Test",
            Assets = [new CreateWatchlistAssetRequest("aapl", "Apple")]
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AAPL", result.Value.Assets[0].Symbol);
    }

    [Fact]
    public async Task Handle_Creates_Watchlist_With_Multiple_Assets()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateWatchlist.Command
        {
            UserId = 1,
            Name = "Multi Asset",
            Assets =
            [
                new CreateWatchlistAssetRequest("AAPL", "Apple"),
                new CreateWatchlistAssetRequest("MSFT", "Microsoft"),
                new CreateWatchlistAssetRequest("GOOG", "Google")
            ]
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Assets.Count);
    }

    [Fact]
    public async Task Handle_Creates_Watchlist_With_No_Assets()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateWatchlist.Command
        {
            UserId = 1,
            Name = "Empty Watchlist",
            Assets = []
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Assets);
    }

    [Fact]
    public async Task Handle_Calls_SaveChangesAsync()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateWatchlist.Command
        {
            UserId = 1,
            Name = "Test",
            Assets = []
        };

        await _handler.Handle(command, CancellationToken.None);

        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Returns_Empty_EnabledDetectors_For_New_Assets()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateWatchlist.Command
        {
            UserId = 1,
            Name = "Test",
            Assets = [new CreateWatchlistAssetRequest("AAPL", "Apple")]
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Assets[0].EnabledDetectors);
    }
}
