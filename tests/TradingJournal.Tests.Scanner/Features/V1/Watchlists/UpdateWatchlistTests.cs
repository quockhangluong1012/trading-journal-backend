using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Dto;
using TradingJournal.Modules.Scanner.Features.V1.Watchlists;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Watchlists;

public class UpdateWatchlistValidatorTests
{
    private static readonly UpdateWatchlist.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_WatchlistId_Is_Zero()
    {
        var cmd = new UpdateWatchlist.Command { WatchlistId = 0, Name = "Valid" };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.WatchlistId);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var cmd = new UpdateWatchlist.Command { WatchlistId = 1, Name = "" };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Name_Exceeds_MaxLength()
    {
        var cmd = new UpdateWatchlist.Command { WatchlistId = 1, Name = new string('A', 101) };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Valid()
    {
        var cmd = new UpdateWatchlist.Command { WatchlistId = 1, Name = "Updated" };
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Have_Error_When_Asset_Symbol_Is_Empty()
    {
        var cmd = new UpdateWatchlist.Command
        {
            WatchlistId = 1, Name = "Valid",
            Assets = [new CreateWatchlistAssetRequest("", "Display")]
        };
        var result = _validator.TestValidate(cmd);
        Assert.True(result.Errors.Count > 0);
    }
}

public class UpdateWatchlistHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly UpdateWatchlist.Handler _handler;

    public UpdateWatchlistHandlerTests()
    {
        _handler = new UpdateWatchlist.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Watchlist_Not_Found()
    {
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);

        var cmd = new UpdateWatchlist.Command { UserId = 1, WatchlistId = 999, Name = "Updated" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("WatchlistNotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Wrong_User()
    {
        var watchlists = new List<Watchlist>
        {
            new() { Id = 1, UserId = 2, Name = "Other", Assets = new List<WatchlistAsset>() }
        };
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(watchlists).Object);

        var cmd = new UpdateWatchlist.Command { UserId = 1, WatchlistId = 1, Name = "Hack" };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Success_And_Updates_Fields()
    {
        var watchlists = new List<Watchlist>
        {
            new() { Id = 1, UserId = 1, Name = "Old", IsActive = true, Assets = new List<WatchlistAsset>() }
        };
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(watchlists).Object);
        _dbMock.Setup(x => x.WatchlistAssets).Returns(DbSetMockHelper.CreateMockDbSet(new List<WatchlistAsset>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new UpdateWatchlist.Command
        {
            UserId = 1, WatchlistId = 1, Name = "New Name", IsActive = false,
            Assets = [new CreateWatchlistAssetRequest("tsla", "Tesla")]
        };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Value.Name);
        Assert.False(result.Value.IsActive);
        Assert.Single(result.Value.Assets);
        Assert.Equal("TSLA", result.Value.Assets[0].Symbol);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
