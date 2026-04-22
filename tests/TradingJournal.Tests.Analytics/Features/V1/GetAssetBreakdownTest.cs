using TradingJournal.Modules.Analytics.Features.V1;
using TradingJournal.Shared.Common.Enum;

namespace TradingJournal.Tests.Analytics.Features.V1;

public class GetAssetBreakdownValidatorTests
{
    private static readonly GetAssetBreakdown.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Filter_Is_Invalid()
    {
        var request = new GetAssetBreakdown.Request((AnalyticsFilter)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Filter_Is_Valid()
    {
        var request = new GetAssetBreakdown.Request(AnalyticsFilter.OneYear);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Filter);
    }
}

public class GetAssetBreakdownHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private GetAssetBreakdown.Handler _handler = null!;
    private const int UserId = 1;

    public GetAssetBreakdownHandlerTests()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _handler = new GetAssetBreakdown.Handler(_tradeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Asset_Groups_When_Closed_Trades_Exist()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1m, Pnl = 50m, StopLoss = 1.098m, TargetTier1 = 1.11m, Status = TradeStatus.Closed, Date = new DateTime(2026, 1, 15), ClosedDate = new DateTime(2026, 1, 16), CreatedBy = UserId },
            new() { Id = 2, Asset = "BTC", Position = PositionType.Long, EntryPrice = 1.1m, Pnl = 30m, StopLoss = 1.098m, TargetTier1 = 1.11m, Status = TradeStatus.Closed, Date = new DateTime(2026, 1, 20), ClosedDate = new DateTime(2026, 1, 21), CreatedBy = UserId },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(trades);
        var request = new GetAssetBreakdown.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.True(result.Value.Any(a => a.Asset == "EURUSD"));
        Assert.True(result.Value.Any(a => a.Asset == "BTC"));
    }

    [Fact]
    public async Task Handle_Returns_Empty_When_No_Closed_Trades()
    {
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        var request = new GetAssetBreakdown.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}


