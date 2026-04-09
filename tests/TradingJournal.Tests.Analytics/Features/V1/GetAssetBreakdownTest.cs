using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Analytics.Features.V1;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Analytics.Features.V1;

[TestFixture]
public class GetAssetBreakdownValidatorTests
{
    private static readonly GetAssetBreakdown.Validator _validator = new();

    [Test]
    public void Should_Have_Error_When_Filter_Is_Invalid()
    {
        var request = new GetAssetBreakdown.Request((AnalyticsFilter)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }

    [Test]
    public void Should_Not_Have_Error_When_Filter_Is_Valid()
    {
        var request = new GetAssetBreakdown.Request(AnalyticsFilter.OneYear);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Filter);
    }
}

[TestFixture]
public class GetAssetBreakdownHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private GetAssetBreakdown.Handler _handler = null!;
    private const int UserId = 1;

    [SetUp]
    public void SetUp()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _handler = new GetAssetBreakdown.Handler(_tradeProviderMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Asset_Groups_When_Closed_Trades_Exist()
    {
        var trades = new List<TradeCacheDto>
        {
            new() { Id = 1, Asset = "EURUSD", Position = PositionType.Long, EntryPrice = 1.1, Pnl = 50m, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = new DateTime(2026, 1, 15), ClosedDate = new DateTime(2026, 1, 16), CreatedBy = UserId },
            new() { Id = 2, Asset = "BTC", Position = PositionType.Long, EntryPrice = 1.1, Pnl = 30m, StopLoss = 1.098, TargetTier1 = 1.11, Status = TradeStatus.Closed, Date = new DateTime(2026, 1, 20), ClosedDate = new DateTime(2026, 1, 21), CreatedBy = UserId },
        };
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trades);
        var request = new GetAssetBreakdown.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Has.Count.EqualTo(2));
        Assert.That(result.Value.Any(a => a.Asset == "EURUSD"), Is.True);
        Assert.That(result.Value.Any(a => a.Asset == "BTC"), Is.True);
    }

    [Test]
    public async Task Handle_Returns_Empty_When_No_Closed_Trades()
    {
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        var request = new GetAssetBreakdown.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.Empty);
    }
}
