using TradingJournal.Modules.Analytics.Features.V1;
using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Analytics.Features.V1;

public class GetInsightsValidatorTests
{
    private static readonly GetInsights.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Filter_Is_Invalid()
    {
        var request = new GetInsights.Request((AnalyticsFilter)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Filter_Is_Valid()
    {
        var request = new GetInsights.Request(AnalyticsFilter.AllTime);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class GetInsightsHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private GetInsights.Handler _handler = null!;
    private const int UserId = 1;

    public GetInsightsHandlerTests()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _handler = new GetInsights.Handler(_tradeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Keep_Trading_Insight_When_No_Closed_Trades()
    {
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        var request = new GetInsights.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Count);
        Assert.Equal("info", result.Value.First().Type);
    }

    [Fact]
    public async Task Handle_Returns_Multiple_Insights_When_Profit_Factor_High()
    {
        var now = DateTime.UtcNow;
        var trades = Enumerable.Range(1, 10).Select(i => new TradeCacheDto
        {
            Id = i, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, Pnl = 100m,
            StopLoss = 1.095, TargetTier1 = 1.12, Status = TradeStatus.Closed,
            Date = now.AddDays(-i - 1), ClosedDate = now.AddDays(-i), CreatedBy = UserId
        }).ToList();
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);
        var request = new GetInsights.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Count > 1);
    }
}
