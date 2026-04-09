using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Analytics.Features.V1;
using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;
using TradingJournal.Shared.Interfaces;

namespace TradingJournal.Tests.Analytics.Features.V1;

[TestFixture]
public class GetInsightsValidatorTests
{
    private static readonly GetInsights.Validator _validator = new();

    [Test]
    public void Should_Have_Error_When_Filter_Is_Invalid()
    {
        var request = new GetInsights.Request((AnalyticsFilter)999);
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Filter);
    }

    [Test]
    public void Should_Not_Have_Error_When_Filter_Is_Valid()
    {
        var request = new GetInsights.Request(AnalyticsFilter.AllTime);
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}

[TestFixture]
public class GetInsightsHandlerTests
{
    private Mock<ITradeProvider> _tradeProviderMock = null!;
    private GetInsights.Handler _handler = null!;
    private const int UserId = 1;

    [SetUp]
    public void SetUp()
    {
        _tradeProviderMock = new Mock<ITradeProvider>();
        _handler = new GetInsights.Handler(_tradeProviderMock.Object);
    }

    [Test]
    public async Task Handle_Returns_Keep_Trading_Insight_When_No_Closed_Trades()
    {
        _tradeProviderMock.Setup(x => x.GetTradesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<TradeCacheDto>());
        var request = new GetInsights.Request(AnalyticsFilter.AllTime, UserId);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Has.Count.EqualTo(1));
        Assert.That(result.Value.First().Type, Is.EqualTo("info"));
    }

    [Test]
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

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Has.Count.GreaterThan(1));
    }
}
