using Moq;
using TradingJournal.Modules.Scanner.Common.Enums;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Features.V1.Scanner;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Scanner;

public class StartScannerHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly StartScanner.Handler _handler;

    public StartScannerHandlerTests()
    {
        _handler = new StartScanner.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Creates_Default_Config_When_None_Exists()
    {
        _dbMock.Setup(x => x.ScannerConfigs).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerConfig>()).Object);
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new StartScanner.Command { UserId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Running", result.Value.Status);
        Assert.Equal(300, result.Value.ScanIntervalSeconds);
        Assert.Equal(1, result.Value.MinConfluenceScore);
        Assert.Equal(17, result.Value.EnabledPatterns.Count); // All 17 ICT patterns
        Assert.Equal(4, result.Value.EnabledTimeframes.Count); // D1, H1, M15, M5
    }

    [Fact]
    public async Task Handle_Sets_Existing_Config_To_Running()
    {
        var config = new ScannerConfig
        {
            Id = 1, UserId = 1, ScanIntervalSeconds = 120, MinConfluenceScore = 2,
            IsRunning = false,
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
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new StartScanner.Command { UserId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Running", result.Value.Status);
        Assert.True(config.IsRunning);
        Assert.Equal(120, result.Value.ScanIntervalSeconds);
    }

    [Fact]
    public async Task Handle_Calls_SaveChangesAsync()
    {
        _dbMock.Setup(x => x.ScannerConfigs).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerConfig>()).Object);
        _dbMock.Setup(x => x.Watchlists).Returns(DbSetMockHelper.CreateMockDbSet(new List<Watchlist>()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _handler.Handle(new StartScanner.Command { UserId = 1 }, CancellationToken.None);

        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
