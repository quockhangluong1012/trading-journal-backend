using TradingJournal.Modules.Analytics.Features.V1;
using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Analytics.Features.V1;

public class GetEquityCurveValidatorTests
{
    private static readonly GetEquityCurve.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Filter_Is_Invalid()
    {
        var request = new GetEquityCurve.Request((AnalyticsFilter)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Filter_Is_Valid()
    {
        var request = new GetEquityCurve.Request(AnalyticsFilter.OneWeek);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Filter);
    }
}

public class GetEquityCurveHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private GetEquityCurve.Handler _handler = null!;
    private const int UserId = 1;

    public GetEquityCurveHandlerTests()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _handler = new GetEquityCurve.Handler(_tradeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Equity_Points_When_Closed_Trades_Exist()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, ExitPrice = 1.105, Pnl = 50, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = now.AddDays(-3), ClosedDate = now.AddDays(-2), CreatedBy = UserId },
            new() { Id = 2, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, ExitPrice = 1.105, Pnl = 30, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = now.AddDays(-2), ClosedDate = now.AddDays(-1), CreatedBy = UserId },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);

        var request = new GetEquityCurve.Request(AnalyticsFilter.AllTime, UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        // Equity is cumulative: 50, then 50+30=80
        Assert.Equal(50, result.Value.ElementAt(0).Profit);
        Assert.Equal(80, result.Value.ElementAt(1).Profit);
    }

    [Fact]
    public async Task Handle_Returns_Empty_When_No_Closed_Trades()
    {
        // Arrange
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        var request = new GetEquityCurve.Request(AnalyticsFilter.AllTime, UserId);

        // Act
        var result = await _handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
