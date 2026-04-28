using Moq;
using TradingJournal.Modules.Scanner.Common.Enums;
using TradingJournal.Modules.Scanner.Domain;
using TradingJournal.Modules.Scanner.Features.V1.Alerts;
using TradingJournal.Modules.Scanner.Infrastructure;
using TradingJournal.Tests.Scanner.Helpers;

namespace TradingJournal.Tests.Scanner.Features.V1.Alerts;

public class DismissAlertHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly DismissAlert.Handler _handler;

    public DismissAlertHandlerTests()
    {
        _handler = new DismissAlert.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Alert_Not_Found()
    {
        _dbMock.Setup(x => x.ScannerAlerts).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerAlert>()).Object);

        var cmd = new DismissAlert.Command { UserId = 1, AlertId = 999 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AlertNotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Wrong_User()
    {
        var alerts = new List<ScannerAlert>
        {
            new()
            {
                Id = 1, UserId = 2, Symbol = "AAPL",
                PatternType = IctPatternType.FVG, Timeframe = ScannerTimeframe.H1,
                DetectionTimeframe = ScannerTimeframe.H1, PriceAtDetection = 150m,
                Description = "Test", ConfluenceScore = 1, DetectedAt = DateTime.UtcNow
            }
        };
        _dbMock.Setup(x => x.ScannerAlerts).Returns(DbSetMockHelper.CreateMockDbSet(alerts).Object);

        var cmd = new DismissAlert.Command { UserId = 1, AlertId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Dismisses_Alert_Successfully()
    {
        var alert = new ScannerAlert
        {
            Id = 1, UserId = 1, Symbol = "AAPL",
            PatternType = IctPatternType.FVG, Timeframe = ScannerTimeframe.H1,
            DetectionTimeframe = ScannerTimeframe.H1, PriceAtDetection = 150m,
            Description = "FVG detected", ConfluenceScore = 2, DetectedAt = DateTime.UtcNow,
            IsDismissed = false
        };
        _dbMock.Setup(x => x.ScannerAlerts).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerAlert> { alert }).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cmd = new DismissAlert.Command { UserId = 1, AlertId = 1 };
        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.True(alert.IsDismissed);
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetAlertsHandlerTests
{
    private readonly Mock<IScannerDbContext> _dbMock = new();
    private readonly GetAlerts.Handler _handler;

    public GetAlertsHandlerTests()
    {
        _handler = new GetAlerts.Handler(_dbMock.Object);
    }

    private static ScannerAlert CreateAlert(int id, int userId, bool isDismissed = false, bool isDisabled = false)
    {
        return new ScannerAlert
        {
            Id = id, UserId = userId, Symbol = "AAPL",
            PatternType = IctPatternType.FVG, Timeframe = ScannerTimeframe.H1,
            DetectionTimeframe = ScannerTimeframe.M15, PriceAtDetection = 150m,
            Description = $"Alert {id}", ConfluenceScore = 1,
            DetectedAt = DateTime.UtcNow.AddMinutes(-id),
            IsDismissed = isDismissed, IsDisabled = isDisabled
        };
    }

    [Fact]
    public async Task Handle_Returns_Empty_List_When_No_Alerts()
    {
        _dbMock.Setup(x => x.ScannerAlerts).Returns(DbSetMockHelper.CreateMockDbSet(new List<ScannerAlert>()).Object);

        var req = new GetAlerts.Request { UserId = 1, ActiveOnly = false, Page = 1, PageSize = 20 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_Returns_Only_User_Alerts()
    {
        var alerts = new List<ScannerAlert>
        {
            CreateAlert(1, userId: 1),
            CreateAlert(2, userId: 2),
            CreateAlert(3, userId: 1)
        };
        _dbMock.Setup(x => x.ScannerAlerts).Returns(DbSetMockHelper.CreateMockDbSet(alerts).Object);

        var req = new GetAlerts.Request { UserId = 1, ActiveOnly = false, Page = 1, PageSize = 20 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task Handle_Filters_Dismissed_When_ActiveOnly()
    {
        var alerts = new List<ScannerAlert>
        {
            CreateAlert(1, userId: 1, isDismissed: false),
            CreateAlert(2, userId: 1, isDismissed: true),
            CreateAlert(3, userId: 1, isDismissed: false)
        };
        _dbMock.Setup(x => x.ScannerAlerts).Returns(DbSetMockHelper.CreateMockDbSet(alerts).Object);

        var req = new GetAlerts.Request { UserId = 1, ActiveOnly = true, Page = 1, PageSize = 20 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, a => Assert.False(a.IsDismissed));
    }

    [Fact]
    public async Task Handle_Excludes_Disabled_Alerts()
    {
        var alerts = new List<ScannerAlert>
        {
            CreateAlert(1, userId: 1),
            CreateAlert(2, userId: 1, isDisabled: true)
        };
        _dbMock.Setup(x => x.ScannerAlerts).Returns(DbSetMockHelper.CreateMockDbSet(alerts).Object);

        var req = new GetAlerts.Request { UserId = 1, ActiveOnly = false, Page = 1, PageSize = 20 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task Handle_Paginates_Results()
    {
        var alerts = Enumerable.Range(1, 10).Select(i => CreateAlert(i, userId: 1)).ToList();
        _dbMock.Setup(x => x.ScannerAlerts).Returns(DbSetMockHelper.CreateMockDbSet(alerts).Object);

        var req = new GetAlerts.Request { UserId = 1, ActiveOnly = false, Page = 2, PageSize = 3 };
        var result = await _handler.Handle(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
    }
}
