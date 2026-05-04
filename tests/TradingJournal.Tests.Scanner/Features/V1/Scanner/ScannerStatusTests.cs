using Moq;
using TradingJournal.Modules.Scanner.Common.Enums;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Features.V1.Scanner;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Scanner;

public class StopScannerHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly StopScanner.Handler _handler;

    public StopScannerHandlerTests()
    {
        _handler = new StopScanner.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Config_Not_Found()
    {
        _dbMock.Setup(x => x.ScannerConfigs).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerConfig>()).Object);

        var cmd = new StopScanner.Command { UserId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("ConfigNotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Stops_Running_Scanner()
    {
        var config = new ScannerConfig
        {
            Id = 1, UserId = 1, ScanIntervalSeconds = 300, MinConfluenceScore = 1,
            IsRunning = true,
            EnabledPatterns = new List<ScannerConfigPattern>
            {
                new() { Id = 1, PatternType = IctPatternType.FVG }
            },
            EnabledTimeframes = new List<ScannerConfigTimeframe>
            {
                new() { Id = 1, Timeframe = ScannerTimeframe.H1 }
            }
        };
        _dbMock.Setup(x => x.ScannerConfigs).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerConfig> { config }).Object);
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>
        {
            new() { Id = 1, UserId = 1, Name = "Test", IsActive = true, IsScannerRunning = true }
        }).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new StopScanner.Command { UserId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Stopped", result.Value.Status);
        Assert.False(config.IsRunning);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetScannerStatusHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly GetScannerStatus.Handler _handler;

    public GetScannerStatusHandlerTests()
    {
        _handler = new GetScannerStatus.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Default_Status_When_No_Config()
    {
        _dbMock.Setup(x => x.ScannerConfigs).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerConfig>()).Object);

        var req = new GetScannerStatus.Request { UserId = 1 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Stopped", result.Value.Status);
        Assert.Equal(300, result.Value.ScanIntervalSeconds);
        Assert.Empty(result.Value.EnabledPatterns);
        Assert.Empty(result.Value.EnabledTimeframes);
        Assert.Equal(1, result.Value.MinConfluenceScore);
    }

    [Fact]
    public async Task Handle_Returns_Running_Status_When_Active()
    {
        var config = new ScannerConfig
        {
            Id = 1, UserId = 1, ScanIntervalSeconds = 120, MinConfluenceScore = 3,
            IsRunning = true,
            EnabledPatterns = new List<ScannerConfigPattern>
            {
                new() { Id = 1, PatternType = IctPatternType.FVG },
                new() { Id = 2, PatternType = IctPatternType.OrderBlock }
            },
            EnabledTimeframes = new List<ScannerConfigTimeframe>
            {
                new() { Id = 1, Timeframe = ScannerTimeframe.H1 },
                new() { Id = 2, Timeframe = ScannerTimeframe.D1 }
            }
        };
        _dbMock.Setup(x => x.ScannerConfigs).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerConfig> { config }).Object);

        // Handler determines status from Watchlists.IsScannerRunning, not config.IsRunning
        var watchlist = new Watchlist
        {
            Id = 1, UserId = 1, Name = "Test", IsActive = true, IsScannerRunning = true,
            Assets = new List<WatchlistAsset>
            {
                new() { Id = 1, Symbol = "EURUSD" }
            }
        };
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist> { watchlist }).Object);

        var req = new GetScannerStatus.Request { UserId = 1 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Running", result.Value.Status);
        Assert.Equal(120, result.Value.ScanIntervalSeconds);
        Assert.Equal(3, result.Value.MinConfluenceScore);
        Assert.Equal(2, result.Value.EnabledPatterns.Count);
        Assert.Equal(2, result.Value.EnabledTimeframes.Count);
    }

    [Fact]
    public async Task Handle_Returns_Stopped_Status_When_Not_Running()
    {
        var config = new ScannerConfig
        {
            Id = 1, UserId = 1, ScanIntervalSeconds = 300, MinConfluenceScore = 1,
            IsRunning = false,
            EnabledPatterns = new List<ScannerConfigPattern>(),
            EnabledTimeframes = new List<ScannerConfigTimeframe>()
        };
        _dbMock.Setup(x => x.ScannerConfigs).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerConfig> { config }).Object);
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);

        var req = new GetScannerStatus.Request { UserId = 1 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Stopped", result.Value.Status);
    }
}
